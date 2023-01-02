using Account.Application.ViewModels.Responses;


public record UserResponse
    {
    public int UserId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string EmailAddress { get; set; }
    public UserStatusResponse UserStatus { get; set; }
    public int NumOfSapAccounts { get; set; }
    public List<string> Roles { get; set; }
    public DateTime? LastLoginDate { get; set; }

    public UserResponse(int id, string firstName, string lastName, string emailAddress, UserStatusResponse status, int numberOfSapAccounts, List<string> roles, DateTime? userLoginDate)
    {
        UserId = id;
        FirstName = firstName;
        LastName = lastName;
        EmailAddress = emailAddress;
        UserStatus = status;
        NumOfSapAccounts = numberOfSapAccounts;
        Roles = roles;
        LastLoginDate = userLoginDate;
    }
}