import { SearchWatchlistCandidatesRequestDto, SearchWatchlistCandidatesResponseDto, GetWatchlistRequestDto, GetWatchlistResponseDto, AddWatchlistItemRequestDto, AddWatchlistItemResponseDto, DeleteWatchlistItemRequestDto, OperationResultDto, GetWatchlistItemDetailRequestDto, GetWatchlistItemDetailResponseDto } from "./AppDtos";
import ApiClient, { ApiClientRequestOptions } from "./ApiClient";

const SearchWatchlistCandidates = (request: SearchWatchlistCandidatesRequestDto, options?: ApiClientRequestOptions): Promise<SearchWatchlistCandidatesResponseDto> =>
  ApiClient.invokeMethod<SearchWatchlistCandidatesResponseDto>("Api", "WatchlistManager", "SearchWatchlistCandidates", request, options);

const GetWatchlist = (request: GetWatchlistRequestDto, options?: ApiClientRequestOptions): Promise<GetWatchlistResponseDto> =>
  ApiClient.invokeMethod<GetWatchlistResponseDto>("Api", "WatchlistManager", "GetWatchlist", request, options);

const AddWatchlistItem = (request: AddWatchlistItemRequestDto, options?: ApiClientRequestOptions): Promise<AddWatchlistItemResponseDto> =>
  ApiClient.invokeMethod<AddWatchlistItemResponseDto>("Api", "WatchlistManager", "AddWatchlistItem", request, options);

const DeleteWatchlistItem = (request: DeleteWatchlistItemRequestDto, options?: ApiClientRequestOptions): Promise<OperationResultDto> =>
  ApiClient.invokeMethod<OperationResultDto>("Api", "WatchlistManager", "DeleteWatchlistItem", request, options);

const GetWatchlistItemDetail = (request: GetWatchlistItemDetailRequestDto, options?: ApiClientRequestOptions): Promise<GetWatchlistItemDetailResponseDto> =>
  ApiClient.invokeMethod<GetWatchlistItemDetailResponseDto>("Api", "WatchlistManager", "GetWatchlistItemDetail", request, options);

export default {
  SearchWatchlistCandidates,
  GetWatchlist,
  AddWatchlistItem,
  DeleteWatchlistItem,
  GetWatchlistItemDetail
};
