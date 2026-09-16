export interface AddWatchlistItemRequestDto {
  Code: string;
  ThsCode: string;
  AssetType: string;
  Name?: string;
  SecurityType?: string;
}

export interface AddWatchlistItemResponseDto {
  Success: boolean;
  AlreadyExists: boolean;
  Message: string;
  Item: WatchlistItemSummaryDto | null;
}

export interface ChangePasswordRequestDto {
  NewPassword: string;
}

export interface DeleteWatchlistItemRequestDto {
  ThsCode: string;
}

export interface DrawdownPointDto {
  Date: string;
  Price: number;
  Drawdown: number;
}

export interface DrawdownRangeOptionDto {
  Value: string;
  Label: string;
}

export interface GetSessionRequestDto {
}

export interface GetWatchlistItemDetailRequestDto {
  ThsCode: string;
  Range: string;
  StartDate?: string;
  EndDate?: string;
}

export interface GetWatchlistItemDetailResponseDto {
  Success: boolean;
  Message: string;
  Item: WatchlistItemSummaryDto | null;
  SelectedRange: string;
  AvailableRanges: DrawdownRangeOptionDto[];
  DrawdownSeries: DrawdownPointDto[];
}

export interface GetWatchlistRequestDto {
}

export interface GetWatchlistResponseDto {
  Items: WatchlistItemSummaryDto[];
}

export interface LoginRequestDto {
  Email: string;
  Password: string;
}

export interface LoginResponseDto {
  IsSuccess: boolean;
  ErrorMessage: string | null;
  AccessToken: string | null;
  RefreshToken: string | null;
  Session: SessionDto | null;
}

export interface OperationResultDto {
  Success: boolean;
  Message: string;
}

export interface SearchWatchlistCandidatesRequestDto {
  Query: string;
}

export interface SearchWatchlistCandidatesResponseDto {
  Items: WatchlistSearchCandidateDto[];
  Message: string;
}

export interface SendPasswordResetRequestDto {
  Email: string;
  RedirectUrl: string | null;
}

export interface ServiceInvocationRequestDto {
  ManagerName: string;
  MethodName: string;
  Parameters: (any | null)[] | null;
  AccessToken: string | null;
  RefreshToken: string | null;
}

export interface ServiceInvocationResponseEnvelopeDto {
  Result: any | null;
  AccessToken: string | null;
  RefreshToken: string | null;
  Session: SessionDto | null;
}

export interface ServiceStreamingRequestDto {
  ManagerName: string;
  MethodName: string;
  Parameters: (any | null)[] | null;
  AccessToken: string | null;
  RefreshToken: string | null;
}

export interface SessionDto {
  UserId: string | null;
  Email: string | null;
  Roles: string[];
}

export interface SignUpRequestDto {
  Email: string;
  Password: string;
  Name: string | null;
}

export interface SignUpResponseDto {
  IsSuccess: boolean;
  ErrorMessage: string | null;
  RequiresFollowUp: boolean;
  AccessToken: string | null;
  RefreshToken: string | null;
  Session: SessionDto | null;
}

export interface StreamingAuthMetaEventDto {
  Type: string;
  AccessToken: string | null;
  RefreshToken: string | null;
  Session: SessionDto | null;
}

export interface UpdateUserEmailDto {
  NewEmail: string;
}

export interface UpdateUserEmailResponseDto {
  Success: boolean;
  Message: string;
}

export interface UpdateUserNameDto {
  NewName: string;
}

export interface UpdateUserNameResponseDto {
  Success: boolean;
  Message: string;
}

export interface UpdateUserPasswordDto {
  NewPassword: string;
}

export interface UpdateUserPasswordResponseDto {
  Success: boolean;
  Message: string;
}

export interface WatchlistItemSummaryDto {
  ThsCode: string;
  Code: string;
  Name: string;
  SecurityType: string;
  AssetType: string;
  CurrentPrice: number | null;
  MaxDrawdown: number | null;
  DataWarning: string | null;
  LastUpdatedUtc: string;
}

export interface WatchlistSearchCandidateDto {
  ThsCode: string;
  Code: string;
  Name: string;
  AssetType: string;
  SecurityType: string;
}
