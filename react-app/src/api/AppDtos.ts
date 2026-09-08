import { SampleEnum } from "./Enums";

export interface AddWatchlistItemRequestDto {
  Code: string;
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
  Code: string;
}

export interface DrawdownPointDto {
  Date: string;
  Price: number;
  Drawdown: number;
}

export interface GetSessionRequestDto {
}

export interface GetWatchlistItemDetailRequestDto {
  Code: string;
}

export interface GetWatchlistItemDetailResponseDto {
  Success: boolean;
  Message: string;
  Item: WatchlistItemSummaryDto | null;
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
  Code: string;
  Name: string;
  SecurityType: string;
  CurrentPrice: number | null;
  MaxDrawdown: number | null;
  DataWarning: string | null;
  LastUpdatedUtc: string;
}

