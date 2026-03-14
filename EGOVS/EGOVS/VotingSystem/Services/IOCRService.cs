using System.Threading.Tasks;
using VotingSystem.Models;

namespace VotingSystem.Services
{
    public interface IOCRService
    {
        Task<OCRResult> ExtractTextFromImageAsync(string imageData);
        Task<EthiopianIDData> ProcessEthiopianIDAsync(string frontImageData, string backImageData);
    }

    public class OCRResult
    {
        public string Text { get; set; }
        public float Confidence { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}