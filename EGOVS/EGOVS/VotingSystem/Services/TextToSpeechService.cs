using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VotingSystem.Services
{
    public class TextToSpeechService
    {
        private readonly ILogger<TextToSpeechService> _logger;
        private readonly string _audioBasePath;

        public TextToSpeechService(ILogger<TextToSpeechService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _audioBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "audio");
            
            if (!Directory.Exists(_audioBasePath))
                Directory.CreateDirectory(_audioBasePath);
        }

        public async Task<AudioInstructions> GenerateRegistrationInstructionsAsync()
        {
            try
            {
                var instructions = new List<string>
                {
                    "Welcome to the Ethiopian Online Voting System registration.",
                    "Please have your national ID card ready.",
                    "We will guide you through the registration process step by step.",
                    "First, we need to capture your national ID information.",
                    "Please position your national ID card in front of the camera.",
                    "Click the camera button when you are ready to capture the front of your ID card."
                };

                var audioFile = await GenerateAudioFileAsync(string.Join(" ", instructions), "welcome_instructions");
                
                return new AudioInstructions
                {
                    AudioUrl = $"/audio/{audioFile}",
                    Text = string.Join(" ", instructions),
                    NextAction = "capture_id_front"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating registration instructions");
                throw;
            }
        }

        public async Task<SpeechProcessResult> ProcessRegistrationStepAsync(string currentStep, string speechText)
        {
            try
            {
                switch (currentStep.ToLower())
                {
                    case "national_id":
                        return await ProcessNationalIdStep(speechText);
                    
                    case "face_verification":
                        return await ProcessFaceVerificationStep(speechText);
                    
                    case "personal_info":
                        return await ProcessPersonalInfoStep(speechText);
                    
                    default:
                        return new SpeechProcessResult 
                        { 
                            Success = false, 
                            Message = "Unknown step",
                            NextStep = "error" 
                        };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing speech step: {currentStep}");
                return new SpeechProcessResult 
                { 
                    Success = false, 
                    Message = "Error processing your response",
                    NextStep = "error" 
                };
            }
        }

        private async Task<SpeechProcessResult> ProcessNationalIdStep(string speechText)
        {
            // Process speech for national ID
            var nationalId = ExtractNationalIdFromSpeech(speechText);
            
            if (string.IsNullOrEmpty(nationalId))
            {
                var retryAudio = await GenerateAudioFileAsync(
                    "I couldn't understand your national ID. Please say your national ID number clearly.", 
                    "national_id_retry");
                
                return new SpeechProcessResult
                {
                    Success = false,
                    Message = "Could not understand national ID",
                    AudioUrl = $"/audio/{retryAudio}",
                    NextStep = "national_id"
                };
            }

            var nextInstruction = await GenerateAudioFileAsync(
                "Thank you. Now we need to verify your identity using face recognition. Please look directly at the camera and click the capture button when you are ready.",
                "face_verification_instruction");

            return new SpeechProcessResult
            {
                Success = true,
                ExtractedData = nationalId,
                AudioUrl = $"/audio/{nextInstruction}",
                NextStep = "face_verification"
            };
        }

        private async Task<SpeechProcessResult> ProcessFaceVerificationStep(string speechText)
        {
            if (speechText.ToLower().Contains("ready") || speechText.ToLower().Contains("yes"))
            {
                return new SpeechProcessResult
                {
                    Success = true,
                    Message = "Ready for face capture",
                    NextStep = "capture_face"
                };
            }
            else
            {
                var retryAudio = await GenerateAudioFileAsync(
                    "Please say 'ready' when you are prepared for face capture.",
                    "face_capture_retry");
                
                return new SpeechProcessResult
                {
                    Success = false,
                    AudioUrl = $"/audio/{retryAudio}",
                    NextStep = "face_verification"
                };
            }
        }

        private async Task<SpeechProcessResult> ProcessPersonalInfoStep(string speechText)
        {
            // This would process personal information from speech
            // For now, return success to continue the flow
            return new SpeechProcessResult
            {
                Success = true,
                Message = "Personal information processed",
                NextStep = "password_setup"
            };
        }

        private string ExtractNationalIdFromSpeech(string speechText)
        {
            // Extract numbers from speech text
            var numbers = System.Text.RegularExpressions.Regex.Matches(speechText, @"\d+");
            if (numbers.Count > 0)
            {
                return string.Join("", numbers.Select(n => n.Value));
            }
            return null;
        }

        private async Task<string> GenerateAudioFileAsync(string text, string fileName)
        {
            try
            {
                // Note: In a real implementation, you would use a TTS service like:
                // - Azure Cognitive Services
                // - Google Text-to-Speech
                // - Amazon Polly
                // - System.Speech.Synthesis (Windows only)

                // For this example, we'll create a placeholder implementation
                // In production, replace this with actual TTS service calls

                var fullPath = Path.Combine(_audioBasePath, $"{fileName}.wav");
                
                // Placeholder: Simulate audio file generation
                await Task.Delay(100);
                
                _logger.LogInformation($"Generated audio for: {text.Substring(0, Math.Min(50, text.Length))}...");
                
                return $"{fileName}.wav";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audio file");
                throw;
            }
        }
    }

    public class AudioInstructions
    {
        public string AudioUrl { get; set; }
        public string Text { get; set; }
        public string NextAction { get; set; }
    }

    public class SpeechProcessResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string AudioUrl { get; set; }
        public string NextStep { get; set; }
        public string ExtractedData { get; set; }
    }
}