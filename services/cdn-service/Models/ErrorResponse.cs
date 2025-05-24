namespace CdnService.Models
{
    public class ErrorResponse
    {
        public string RequestId { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string ErrorDescription { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        
        public ErrorResponse() { }
        
        public ErrorResponse(string requestId, string error, string errorDescription, string? stackTrace = null)
        {
            RequestId = requestId;
            Error = error;
            ErrorDescription = errorDescription;
            StackTrace = stackTrace;
        }
    }
}
