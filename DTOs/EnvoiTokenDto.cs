namespace RecouvrementAPI.DTOs
{
    public class EnvoiTokenDto
    {
        public string Canal { get; set; } // sms ou email
    }

    public class EnvoiTokenResponseDto
    {
        public string Message { get; set; }
        public string TokenGenere { get; set; }
        public string LienPaiement { get; set; }
    }
}
