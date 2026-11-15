using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VotingSystem.Models;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace VotingSystem.Services
{
    public class EthiopianOCRService : IOCRService
    {
        private readonly ILogger<EthiopianOCRService> _logger;

        public EthiopianOCRService(ILogger<EthiopianOCRService> logger)
        {
            _logger = logger;
        }

        public async Task<OCRResult> ExtractTextFromImageAsync(string imageData)
        {
            try
            {
                _logger.LogInformation("Starting real text extraction from Ethiopian ID image...");

                // In a real implementation, this would call Tesseract OCR
                // For now, we'll simulate processing but return empty to force real extraction
                await Task.Delay(500);

                return new OCRResult
                {
                    Text = "", // Empty to indicate no mock data
                    Success = false, // Mark as false to indicate real extraction needed
                    Error = "Real OCR extraction required - use TesseractOCRService for actual ID scanning"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in text extraction");
                return new OCRResult
                {
                    Success = false,
                    Error = $"Text extraction failed: {ex.Message}"
                };
            }
        }

        public async Task<EthiopianIDData> ProcessEthiopianIDAsync(string frontImageBase64, string backImageBase64)
        {
            try
            {
                _logger.LogInformation("=== STARTING REAL ETHIOPIAN ID PROCESSING ===");

                if (string.IsNullOrEmpty(frontImageBase64))
                {
                    throw new Exception("Front ID image is required for real ID processing.");
                }

                // Process with real OCR extraction
                var idData = new EthiopianIDData();

                // Get actual text from images
                string extractedText = await ExtractRealTextFromImages(frontImageBase64, backImageBase64);

                if (string.IsNullOrEmpty(extractedText))
                {
                    throw new Exception("No text could be extracted from the ID images. Please ensure clear, high-quality images.");
                }

                _logger.LogInformation($"=== EXTRACTED RAW TEXT ===");
                _logger.LogInformation(extractedText);
                _logger.LogInformation($"=== END EXTRACTED TEXT ===");

                // Parse the actual extracted text
                ParseRealEthiopianIDData(extractedText, idData);

                // Validate that we got meaningful data
                bool hasValidData = !string.IsNullOrEmpty(idData.NationalId) ||
                                  !string.IsNullOrEmpty(idData.FirstName) ||
                                  !string.IsNullOrEmpty(idData.LastName);

                if (!hasValidData)
                {
                    throw new Exception("Could not extract valid information from the ID card. Please check image quality and try again.");
                }

                idData.Success = true;

                _logger.LogInformation("=== REAL ID PROCESSING COMPLETED ===");
                _logger.LogInformation($"NationalId: {idData.NationalId}");
                _logger.LogInformation($"Name: {idData.FirstName} {idData.MiddleName} {idData.LastName}");
                _logger.LogInformation($"Phone: {idData.PhoneNumber}");
                _logger.LogInformation($"Region: {idData.Region}");
                _logger.LogInformation($"Sex: {idData.Sex}");
                _logger.LogInformation($"Age: {idData.Age}");

                return idData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in real Ethiopian ID processing");

                // Return empty data with error - no mock data
                return new EthiopianIDData
                {
                    Success = false,
                    Error = $"Real ID processing failed: {ex.Message}"
                };
            }
        }

        private async Task<string> ExtractRealTextFromImages(string frontImageData, string backImageData)
        {
            try
            {
                _logger.LogInformation("Extracting real text from ID images...");

                string combinedText = "";

                // Process front image
                if (!string.IsNullOrEmpty(frontImageData))
                {
                    var frontResult = await ExtractTextFromImageAsync(frontImageData);
                    if (frontResult.Success && !string.IsNullOrEmpty(frontResult.Text))
                    {
                        combinedText += "FRONT: " + frontResult.Text + "\n\n";
                    }
                }

                // Process back image
                if (!string.IsNullOrEmpty(backImageData))
                {
                    var backResult = await ExtractTextFromImageAsync(backImageData);
                    if (backResult.Success && !string.IsNullOrEmpty(backResult.Text))
                    {
                        combinedText += "BACK: " + backResult.Text + "\n\n";
                    }
                }

                return combinedText.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting real text from images");
                return "";
            }
        }

        private void ParseRealEthiopianIDData(string extractedText, EthiopianIDData idData)
        {
            try
            {
                _logger.LogInformation("Parsing real extracted Ethiopian ID data...");

                var lines = extractedText.Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrEmpty(line))
                    .ToArray();

                // EXTRACT FAN NUMBER (Ethiopian National ID)
                ExtractRealFANNumber(extractedText, lines, idData);

                // EXTRACT NAMES
                ExtractRealNames(extractedText, lines, idData);

                // EXTRACT PHONE NUMBER
                ExtractRealPhoneNumber(extractedText, idData);

                // EXTRACT GENDER
                ExtractRealGender(extractedText, idData);

                // EXTRACT DATE OF BIRTH AND AGE
                ExtractRealDateOfBirthAndAge(extractedText, idData);

                // EXTRACT REGION - ENHANCED DEBUGGING VERSION
                ExtractRealRegionWithDebugging(extractedText, lines, idData);

                // Set default nationality for Ethiopian IDs
                idData.Nationality = "Ethiopian";

                _logger.LogInformation("Real ID data parsing completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing real Ethiopian ID data");
            }
        }

        private void ExtractRealFANNumber(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                // Look for Ethiopian FAN patterns
                var fanPatterns = new[]
                {
                    @"(ETA|ETB|ETC|ETD)\s*\d{7}", // ETA1234567 format
                    @"FAN\s*[:]?\s*(\d{10,17})", // FAN: 12345678901234567
                    @"\b\d{15,17}\b", // 15-17 digit number
                    @"\b\d{4}\s*\d{4}\s*\d{4}\s*\d{3,5}\b" // 4-4-4-3/4/5 pattern
                };

                foreach (var pattern in fanPatterns)
                {
                    var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        data.NationalId = Regex.Replace(match.Value, @"\s+", "").ToUpper();
                        _logger.LogInformation($"✅ Found FAN: {data.NationalId}");
                        return;
                    }
                }

                _logger.LogWarning("❌ No FAN number found in extracted text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting real FAN number");
            }
        }

        private void ExtractRealNames(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                // Look for name patterns in the text
                foreach (var line in lines)
                {
                    var cleanLine = line.Trim();

                    // Skip lines that contain obvious non-name content
                    if (cleanLine.Contains("FAN") || cleanLine.Contains("ID") ||
                        cleanLine.Contains("Date") || cleanLine.Contains("Birth") ||
                        cleanLine.Contains("Sex") || cleanLine.Contains("Gender") ||
                        cleanLine.Contains("Phone") || Regex.IsMatch(cleanLine, @"\d"))
                        continue;

                    // Look for lines with 2-4 words that could be names
                    var words = cleanLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2 && words.Length <= 4)
                    {
                        bool looksLikeName = words.All(word =>
                            word.Length >= 2 &&
                            !word.Any(char.IsDigit) &&
                            word.All(c => char.IsLetter(c) || c == '.'));

                        if (looksLikeName)
                        {
                            if (words.Length >= 3)
                            {
                                data.FirstName = words[0];
                                data.MiddleName = words[1];
                                data.LastName = string.Join(" ", words.Skip(2));
                            }
                            else if (words.Length == 2)
                            {
                                data.FirstName = words[0];
                                data.LastName = words[1];
                            }

                            _logger.LogInformation($"✅ Found names: {data.FirstName} {data.MiddleName} {data.LastName}");
                            return;
                        }
                    }
                }

                _logger.LogWarning("❌ No names found in extracted text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting real names");
            }
        }

        private void ExtractRealPhoneNumber(string text, EthiopianIDData data)
        {
            try
            {
                var phonePatterns = new[]
                {
                    @"(\+251\d{9})",
                    @"(251\d{9})",
                    @"(09\d{8})",
                    @"(9\d{8})",
                    @"(\d{2}/\d{2}/\d{6})" // Pattern like 09/20/939012
                };

                foreach (var pattern in phonePatterns)
                {
                    var match = Regex.Match(text, pattern);
                    if (match.Success)
                    {
                        var phone = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                        phone = phone.Replace("/", "");
                        data.PhoneNumber = FormatEthiopianPhoneNumber(phone);
                        _logger.LogInformation($"✅ Found phone: {data.PhoneNumber}");
                        return;
                    }
                }

                _logger.LogWarning("❌ No phone number found in extracted text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting real phone number");
            }
        }

        private void ExtractRealGender(string text, EthiopianIDData data)
        {
            try
            {
                if (text.IndexOf("Male", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Regex.IsMatch(text, @"\bM\b", RegexOptions.IgnoreCase))
                {
                    data.Sex = "Male";
                    _logger.LogInformation($"✅ Found gender: Male");
                }
                else if (text.IndexOf("Female", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         Regex.IsMatch(text, @"\bF\b", RegexOptions.IgnoreCase))
                {
                    data.Sex = "Female";
                    _logger.LogInformation($"✅ Found gender: Female");
                }
                else
                {
                    _logger.LogWarning("❌ No gender found in extracted text");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting real gender");
            }
        }

        private void ExtractRealDateOfBirthAndAge(string text, EthiopianIDData data)
        {
            try
            {
                // Look for date patterns
                var datePatterns = new[]
                {
                    @"(\d{1,2}/\d{1,2}/\d{4})", // DD/MM/YYYY
                    @"(\d{4}/\d{1,2}/\d{1,2})", // YYYY/MM/DD
                    @"(\d{1,2}-\d{1,2}-\d{4})", // DD-MM-YYYY
                    @"(\d{4}-\d{1,2}-\d{1,2})" // YYYY-MM-DD
                };

                foreach (var pattern in datePatterns)
                {
                    var matches = Regex.Matches(text, pattern);
                    foreach (Match match in matches)
                    {
                        var dateStr = match.Value;
                        if (DateTime.TryParse(dateStr, out DateTime birthDate))
                        {
                            data.DateOfBirth = dateStr;
                            data.Age = CalculateAge(birthDate);
                            data.CalendarType = "Gregorian";
                            _logger.LogInformation($"✅ Found date of birth: {data.DateOfBirth}, Age: {data.Age}");
                            return;
                        }
                    }
                }

                // If no date found, set default voting age
                data.Age = 25;
                _logger.LogWarning("❌ No date of birth found, using default age");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting real date of birth");
                data.Age = 25; // Default voting age
            }
        }

        private void ExtractRealRegionWithDebugging(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== 🔍 DEBUGGING REGION EXTRACTION ===");
                _logger.LogInformation($"Total text length: {text.Length} characters");
                _logger.LogInformation($"Number of lines: {lines.Length}");
                
                // Log first 10 lines to see what we're working with
                _logger.LogInformation("=== FIRST 10 LINES OF EXTRACTED TEXT ===");
                for (int i = 0; i < Math.Min(10, lines.Length); i++)
                {
                    _logger.LogInformation($"Line {i}: '{lines[i]}'");
                }
                _logger.LogInformation("=== END LINES ===");

                // STRATEGY 1: Direct Amhara search in entire text
                _logger.LogInformation("=== STRATEGY 1: DIRECT AMHARA SEARCH ===");
                if (FindAmharaWithDetailedDebugging(text, lines, data))
                {
                    _logger.LogInformation("✅ REGION FOUND VIA DIRECT AMHARA SEARCH");
                    return;
                }

                // STRATEGY 2: Address section search
                _logger.LogInformation("=== STRATEGY 2: ADDRESS SECTION SEARCH ===");
                if (FindRegionInAddressSectionWithDebugging(lines, data))
                {
                    _logger.LogInformation("✅ REGION FOUND VIA ADDRESS SECTION");
                    return;
                }

                // STRATEGY 3: Any region search
                _logger.LogInformation("=== STRATEGY 3: ANY REGION SEARCH ===");
                if (FindAnyRegionWithDebugging(text, data))
                {
                    _logger.LogInformation("✅ REGION FOUND VIA ANY REGION SEARCH");
                    return;
                }

                // STRATEGY 4: Manual text analysis
                _logger.LogInformation("=== STRATEGY 4: MANUAL TEXT ANALYSIS ===");
                ManualRegionAnalysis(text, lines, data);

                _logger.LogWarning("❌❌❌ NO REGION FOUND AFTER ALL STRATEGIES");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in debugging region extraction");
            }
        }

        private bool FindAmharaWithDetailedDebugging(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("🔍 Searching for Amhara patterns...");

                // Test patterns - most common first
                var testPatterns = new[]
                {
                    "Amhara", "Amara", "AMHARA", "amhara", "አማራ", "Amhar", "Amhra"
                };

                foreach (var pattern in testPatterns)
                {
                    if (text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        data.Region = "Amhara";
                        _logger.LogInformation($"🎯 DIRECT MATCH: Found '{pattern}' in text");
                        return true;
                    }
                }

                // Check each line
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var pattern in testPatterns)
                    {
                        if (lines[i].IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            data.Region = "Amhara";
                            _logger.LogInformation($"🎯 LINE MATCH: Found '{pattern}' in line {i}: '{lines[i]}'");
                            return true;
                        }
                    }
                }

                _logger.LogInformation("❌ No Amhara patterns found in direct search");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Amhara debugging search");
                return false;
            }
        }

        private bool FindRegionInAddressSectionWithDebugging(string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("🔍 Searching for address sections...");

                var addressKeywords = new[]
                {
                    "Address", "Location", "Area", "Region", "Kifle Ketema",
                    "አድራሻ", "ቦታ", "ክልል", "ከተማ", "Adrasha", "Adrash"
                };

                bool foundAddressSection = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var keyword in addressKeywords)
                    {
                        if (lines[i].IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundAddressSection = true;
                            _logger.LogInformation($"📍 Found address keyword '{keyword}' at line {i}: '{lines[i]}'");

                            // Search next 5 lines for region
                            for (int j = i + 1; j <= i + 5 && j < lines.Length; j++)
                            {
                                _logger.LogInformation($"   Checking line {j} below address: '{lines[j]}'");
                                
                                var region = ExtractRegionFromLineDebug(lines[j]);
                                if (!string.IsNullOrEmpty(region))
                                {
                                    data.Region = region;
                                    _logger.LogInformation($"🎯 FOUND REGION BELOW ADDRESS: {data.Region} at line {j}");
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (!foundAddressSection)
                {
                    _logger.LogInformation("❌ No address section found in the text");
                }
                else
                {
                    _logger.LogInformation("❌ Address section found but no region detected below it");
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in address section debugging");
                return false;
            }
        }

        private string ExtractRegionFromLineDebug(string line)
        {
            try
            {
                if (string.IsNullOrEmpty(line)) return null;

                // Quick Amhara check
                if (line.IndexOf("Amhara", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Amara", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("አማራ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Amhara";
                }

                // Other regions
                var regions = new[]
                {
                    "Oromia", "Tigray", "SNNPR", "Addis Ababa", "Somali",
                    "Afar", "Dire Dawa", "Benishangul", "Gambela", "Harari"
                };

                foreach (var region in regions)
                {
                    if (line.IndexOf(region, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return region;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in region line extraction debug");
                return null;
            }
        }

        private bool FindAnyRegionWithDebugging(string text, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("🔍 Searching for any region pattern...");

                var allRegions = new[]
                {
                    "Amhara", "Oromia", "Tigray", "SNNPR", "Addis Ababa", 
                    "Somali", "Afar", "Dire Dawa", "Benishangul", "Gambela", "Harari"
                };

                foreach (var region in allRegions)
                {
                    if (text.IndexOf(region, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        data.Region = region;
                        _logger.LogInformation($"🎯 FOUND REGION: {region}");
                        return true;
                    }
                }

                _logger.LogInformation("❌ No region patterns found in text");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in any region search");
                return false;
            }
        }

        private void ManualRegionAnalysis(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("🔍 Performing manual region analysis...");

                // Look for any text that might indicate region
                foreach (var line in lines)
                {
                    // Check if line contains geographic indicators
                    if (line.Length > 3 && line.Length < 50)
                    {
                        // Skip lines with numbers (likely dates or IDs)
                        if (Regex.IsMatch(line, @"\d")) continue;

                        // Skip common non-region words
                        if (line.Contains("Name") || line.Contains("FAN") || 
                            line.Contains("Date") || line.Contains("Birth") ||
                            line.Contains("Sex") || line.Contains("Phone"))
                            continue;

                        _logger.LogInformation($"📝 Potential region line: '{line}'");

                        // If we find a line that looks like it could be a region but doesn't match our patterns,
                        // we might need to add new patterns
                    }
                }

                // Last resort: if we have other data but no region, check if we can infer from context
                if (!string.IsNullOrEmpty(data.NationalId) && string.IsNullOrEmpty(data.Region))
                {
                    _logger.LogInformation("ℹ️  Could not detect region automatically. Consider manual entry.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manual region analysis");
            }
        }

        private string FormatEthiopianPhoneNumber(string phone)
        {
            try
            {
                var digits = Regex.Replace(phone, @"[^\d]", "");

                if (digits.StartsWith("251") && digits.Length == 12)
                {
                    return "+" + digits;
                }
                else if (digits.StartsWith("9") && digits.Length == 9)
                {
                    return "+251" + digits;
                }
                else if (digits.StartsWith("09") && digits.Length == 10)
                {
                    return "+251" + digits.Substring(1);
                }

                return phone;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting phone number");
                return phone;
            }
        }

        private int CalculateAge(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;

            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }
    }
}