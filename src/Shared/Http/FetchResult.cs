namespace KPPasskeyChecker.Shared.Http
{
    public sealed class FetchResult
    {
        public FetchOutcome Outcome { get; set; }
        public string Content { get; set; }
        public byte[] ContentBytes { get; set; }
        public string ETag { get; set; }
        public string ErrorMessage { get; set; }

        public static FetchResult Success(string content, string etag)
        {
            return new FetchResult { Outcome = FetchOutcome.Success, Content = content, ETag = etag };
        }

        public static FetchResult Success(byte[] content, string etag)
        {
            return new FetchResult { Outcome = FetchOutcome.Success, ContentBytes = content, ETag = etag };
        }

        public static FetchResult NotModified()
        {
            return new FetchResult { Outcome = FetchOutcome.NotModified };
        }

        public static FetchResult Failed(string error)
        {
            return new FetchResult { Outcome = FetchOutcome.Failed, ErrorMessage = error };
        }
    }
}
