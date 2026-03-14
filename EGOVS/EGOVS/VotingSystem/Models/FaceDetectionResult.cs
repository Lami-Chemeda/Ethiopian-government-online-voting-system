namespace VotingSystem.Models
{
    public class FaceDetectionResult
    {
        public bool Success { get; set; }
        public string FaceHash { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Error { get; set; } = string.Empty;
        public string FaceImageData { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class OCRResult
    {
        public bool Success { get; set; }
        public string Text { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Error { get; set; } = string.Empty;
        public EthiopianIDData ExtractedData { get; set; } = new EthiopianIDData();
    }
}