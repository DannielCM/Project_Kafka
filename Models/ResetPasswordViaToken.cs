namespace MyAuthenticationBackend.Models;
public class ResetPasswordViaToken
{
    public string Token {set; get;} = "";
    public string NewPassword {set; get;} = "";
}