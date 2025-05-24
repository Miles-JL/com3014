namespace CdnService.Models
{
    public class ErrorResponse
    {
        public string RequestId { get; set; }
        public string Error { get; set; }
        public string ErrorDescription { get; set; }
        public string StackTrace { get; set; }
    }
}
