namespace DemoRepo;

public class UserAuth
{
    public bool CheckUser(string user) => !string.IsNullOrWhiteSpace(user);
}
