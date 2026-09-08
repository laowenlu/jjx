using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using App.ServiceInvoker.Auth;
using App.ServiceInvoker.Attributes;
using App.ServiceInvoker.Interfaces;
using Contracts.Dto;
using Microsoft.AspNetCore.Http;

namespace App.ServiceInvoker.Reflection
{
    public class ServiceMethodInvoker
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMethodAuthorizer _methodAuthorizer;
        private readonly IAuthTokenManager _authTokenManager;
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Lazy<MethodMetadata>>> ServiceMethodCache = new();
        private readonly JsonSerializerOptions _jsonOptions;

        public ServiceMethodInvoker(
            IServiceProvider serviceProvider,
            IMethodAuthorizer methodAuthorizer,
            IAuthTokenManager authTokenManager,
            JsonSerializerOptions? jsonOptions = null)
        {
            _serviceProvider = serviceProvider;
            _methodAuthorizer = methodAuthorizer;
            _authTokenManager = authTokenManager;
            _jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                MaxDepth = 256
            };
        }

        public async Task<object?> InvokeAsync(HttpRequest request)
        {
            var requestDto = await request.ReadFromJsonAsync<ServiceInvocationRequestDto>(_jsonOptions);
            if (requestDto == null)
            {
                throw new ArgumentException("Invalid JSON body.");
            }

            var serviceInstance = GetManagerServiceByName(requestDto.ManagerName);
            if (serviceInstance == null)
            {
                throw new InvalidOperationException($"Service '{requestDto.ManagerName}' not found.");
            }

            var methodMetadata = GetOrLoadMethodMetadata(serviceInstance, requestDto.ManagerName, requestDto.MethodName);
            if (methodMetadata.MethodInfo == null)
            {
                throw new MissingMethodException($"Method '{requestDto.MethodName}' not found in service '{requestDto.ManagerName}'.");
            }

            var ensuredTokens = await _authTokenManager.EnsureAuthenticatedRequest(request, requestDto.AccessToken, requestDto.RefreshToken);
            EnforceAuthIfRequired(methodMetadata, ensuredTokens);

            var typedParameters = PrepareParameters(requestDto.Parameters, methodMetadata.MethodInfo);
            object? result = methodMetadata.MethodInfo.Invoke(serviceInstance, typedParameters);
            result = await AwaitIfTaskAsync(result);
            var session = result as SessionDto ?? BuildSession(request.HttpContext);

            return new ServiceInvocationResponseEnvelopeDto
            {
                Result = result,
                AccessToken = ensuredTokens?.AccessToken,
                RefreshToken = ensuredTokens?.RefreshToken,
                Session = session
            };
        }

        public async Task InvokeStreamingAsync(HttpRequest request, Stream responseStream, CancellationToken cancellationToken)
        {
            var requestDto = await request.ReadFromJsonAsync<ServiceStreamingRequestDto>(_jsonOptions, cancellationToken);
            if (requestDto == null)
            {
                throw new ArgumentException("Invalid JSON body for streaming request.");
            }

            var serviceInstance = GetManagerServiceByName(requestDto.ManagerName);
            if (serviceInstance == null)
            {
                throw new InvalidOperationException($"Service '{requestDto.ManagerName}' not found.");
            }

            var methodMetadata = GetOrLoadMethodMetadata(serviceInstance, requestDto.ManagerName, requestDto.MethodName);
            if (methodMetadata.MethodInfo == null)
            {
                throw new MissingMethodException($"Method '{requestDto.MethodName}' not found in service '{requestDto.ManagerName}'.");
            }

            var ensuredTokens = await _authTokenManager.EnsureAuthenticatedRequest(request, requestDto.AccessToken, requestDto.RefreshToken);
            EnforceAuthIfRequired(methodMetadata, ensuredTokens);

            request.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            request.HttpContext.Response.Headers.CacheControl = "no-cache";
            request.HttpContext.Response.ContentType = "application/x-ndjson; charset=utf-8";

            var meta = new StreamingAuthMetaEventDto
            {
                Type = "auth",
                AccessToken = ensuredTokens?.AccessToken,
                RefreshToken = ensuredTokens?.RefreshToken,
                Session = BuildSession(request.HttpContext)
            };
            var authJson = JsonSerializer.Serialize(meta, _jsonOptions);
            var authBytes = Encoding.UTF8.GetBytes(authJson + "\n");
            await responseStream.WriteAsync(authBytes, cancellationToken);
            await responseStream.FlushAsync(cancellationToken);

            var typedParameters = PrepareParameters(requestDto.Parameters, methodMetadata.MethodInfo);
            object? result = methodMetadata.MethodInfo.Invoke(serviceInstance, typedParameters);
            result = await AwaitIfTaskAsync(result);
            if (result == null)
            {
                return;
            }

            var asyncEnumerableInterface = result.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

            if (asyncEnumerableInterface == null)
            {
                throw new InvalidOperationException("Streaming methods must return IAsyncEnumerable<T>.");
            }

            await StreamAsyncEnumerableAsNdjsonAsync(result, responseStream, cancellationToken);
        }

        private void EnforceAuthIfRequired(MethodMetadata metadata, AuthTokenSet? ensuredTokens)
        {
            var requiresAuth = metadata.RequireAuthenticated != null || metadata.RequireRole != null;
            if (!requiresAuth)
            {
                return;
            }

            if (ensuredTokens == null)
            {
                throw new UnauthorizedAccessException("Authentication required.");
            }

            _methodAuthorizer.Authorize(metadata);
        }

        private static SessionDto? BuildSession(HttpContext httpContext)
        {
            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userId = user.Claims.FirstOrDefault(c => c.Type is "sub" or System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = user.Claims.FirstOrDefault(c => c.Type is "email" or System.Security.Claims.ClaimTypes.Email)?.Value;
            var roles = RoleClaimsReader.GetRoles(user).ToList();

            return new SessionDto { UserId = userId, Email = email, Roles = roles };
        }

        private async Task StreamAsyncEnumerableAsNdjsonAsync(object asyncEnumerable, Stream responseStream, CancellationToken cancellationToken)
        {
            await using var writer = new StreamWriter(responseStream, Encoding.UTF8, leaveOpen: true);

            var asyncEnumerableInterface = asyncEnumerable.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

            if (asyncEnumerableInterface == null)
            {
                throw new InvalidOperationException("Streaming methods must return IAsyncEnumerable<T>.");
            }

            var itemType = asyncEnumerableInterface.GetGenericArguments()[0];
            var iAsyncEnumerableType = typeof(IAsyncEnumerable<>).MakeGenericType(itemType);
            var iAsyncEnumeratorType = typeof(IAsyncEnumerator<>).MakeGenericType(itemType);
            var getEnumeratorMethod = iAsyncEnumerableType.GetMethod("GetAsyncEnumerator", new[] { typeof(CancellationToken) });
            if (getEnumeratorMethod == null)
            {
                throw new InvalidOperationException("IAsyncEnumerable<T> did not have a GetAsyncEnumerator(CancellationToken) method.");
            }

            var enumerator = getEnumeratorMethod.Invoke(asyncEnumerable, new object?[] { cancellationToken });
            if (enumerator == null)
            {
                return;
            }

            try
            {
                var moveNextAsync = iAsyncEnumeratorType.GetMethod("MoveNextAsync");
                var currentProp = iAsyncEnumeratorType.GetProperty("Current");
                if (moveNextAsync == null || currentProp == null)
                {
                    throw new InvalidOperationException("IAsyncEnumerator<T> missing MoveNextAsync/Current.");
                }

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var moveNextValueTaskObj = moveNextAsync.Invoke(enumerator, null);
                    if (moveNextValueTaskObj == null)
                    {
                        break;
                    }

                    var asTask = (Task<bool>)moveNextValueTaskObj.GetType().GetMethod("AsTask")!.Invoke(moveNextValueTaskObj, null)!;
                    var hasNext = await asTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (!hasNext) break;

                    var current = currentProp.GetValue(enumerator);
                    var json = JsonSerializer.Serialize(current, _jsonOptions);
                    await writer.WriteAsync(json.AsMemory(), cancellationToken);
                    await writer.WriteAsync("\n".AsMemory(), cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }
            }
            finally
            {
                if (enumerator is IAsyncDisposable d) await d.DisposeAsync();
            }
        }

        private object?[]? PrepareParameters(object?[]? rawParams, MethodInfo methodInfo)
        {
            var paramInfos = methodInfo.GetParameters();
            if (paramInfos.Length == 0)
            {
                return null;
            }

            if (rawParams == null || rawParams.Length != paramInfos.Length)
            {
                throw new ArgumentException($"Parameter count mismatch for method '{methodInfo.Name}'.");
            }

            var finalArgs = new object?[paramInfos.Length];
            for (int i = 0; i < paramInfos.Length; i++)
            {
                var paramType = paramInfos[i].ParameterType;
                var json = JsonSerializer.Serialize(rawParams[i], _jsonOptions);
                finalArgs[i] = JsonSerializer.Deserialize(json, paramType, _jsonOptions);
            }
            return finalArgs;
        }

        private IManagerService? GetManagerServiceByName(string serviceName)
        {
            var serviceType = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName != null && (a.FullName.StartsWith("App") || a.FullName.StartsWith("Contracts")))
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase)
                                     && typeof(IManagerService).IsAssignableFrom(t));

            if (serviceType == null)
            {
                return null;
            }

            return _serviceProvider.GetService(serviceType) as IManagerService;
        }

        private MethodMetadata GetOrLoadMethodMetadata(IManagerService serviceInstance, string managerName, string methodName)
        {
            var managerCacheKey = managerName.ToUpperInvariant();
            var methodCacheKey = methodName.ToUpperInvariant();

            return ServiceMethodCache
                .GetOrAdd(managerCacheKey, _ => new ConcurrentDictionary<string, Lazy<MethodMetadata>>())
                .GetOrAdd(methodCacheKey, new Lazy<MethodMetadata>(() => LoadMethodMetadata(serviceInstance, methodName)))
                .Value;
        }

        private static MethodMetadata LoadMethodMetadata(IManagerService serviceInstance, string methodName)
        {
            var methodInfo = serviceInstance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

            if (methodInfo == null)
            {
                return new MethodMetadata();
            }

            return new MethodMetadata
            {
                MethodInfo = methodInfo,
                RequireAuthenticated = methodInfo.GetCustomAttribute<RequireAuthenticatedAttribute>(),
                RequireRole = methodInfo.GetCustomAttribute<RequireRoleAttribute>()
            };
        }

        private static async Task<object?> AwaitIfTaskAsync(object? result)
        {
            if (result is not Task task)
            {
                return result;
            }

            await task.ConfigureAwait(false);
            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProp = taskType.GetProperty("Result");
                return resultProp?.GetValue(task);
            }

            return null;
        }
    }
}
