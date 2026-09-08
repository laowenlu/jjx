using Contracts.Domain.Enums;
using Contracts.Domain.Logic;

namespace App.Api.Engines
{
    /// <summary>
    /// SampleEngine provides basic, stateless operations over the SampleLogicContract and SampleEnum.
    /// <remarks>
    /// Register this engine for dependency injection in Program.cs:
    /// <code>
    /// services.AddScoped&lt;SampleEngine&gt;();
    /// </code>
    /// </remarks>
    /// </summary>
    public class SampleEngine
    {
        // Example: Inject dependencies via constructor if needed (accessors/utilities only).
        // Uncomment and add parameters as needed for DI:
        //
        // private readonly SomeAccessor _someAccessor;
        // public SampleEngine(SomeAccessor someAccessor)
        // {
        //     _someAccessor = someAccessor;
        // }

        /// <summary>
        /// Creates a new SampleLogicContract from supplied data.
        /// </summary>
        public SampleLogicContract CreateContract(string name, SampleEnum status)
        {
            return new SampleLogicContract
            {
                Name = name,
                Status = status
            };
        }

        /// <summary>
        /// Returns a human-readable description for a SampleEnum value.
        /// </summary>
        public string DescribeStatus(SampleEnum status)
        {
            return status switch
            {
                SampleEnum.None => "No status set.",
                SampleEnum.Alpha => "Alpha status (first).",
                SampleEnum.Beta => "Beta status (second).",
                SampleEnum.Gamma => "Gamma status (third).",
                _ => "Unknown status."
            };
        }

        /// <summary>
        /// Describes the state of a SampleLogicContract for demo or debugging purposes.
        /// </summary>
        public string DescribeContract(SampleLogicContract contract)
        {
            return $"SampleLogicContract: Name='{contract.Name}', Status='{DescribeStatus(contract.Status)}'";
        }
    }
}