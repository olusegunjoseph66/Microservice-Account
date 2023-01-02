using Account.Application.Enums;


public class UserQueryFilter
{
    public string? RoleName { get; set; }
    public string? SearchKeyword { get; set; }
    public string? UserStatusCode { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public UserSortingEnum Sort { get; set; }
}
