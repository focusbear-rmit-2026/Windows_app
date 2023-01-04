namespace Focus_Bear.Models
{
    public class UserAccount
    {
        public string userID { get; set; }
        public string username { get; set; }
        public string email { get; set; }
        public string accessToken { get; set; }
        public string refreshToken { get; set; }
    }
}
