using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tesseract;
using VotingSystem.Models;

namespace VotingSystem.Services
{
    public class TesseractOCRService : IOCRService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TesseractOCRService> _logger;
        private readonly string _tessDataPath;

        public TesseractOCRService(HttpClient httpClient, IConfiguration configuration, ILogger<TesseractOCRService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            _tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
            
            if (!Directory.Exists(_tessDataPath))
            {
                Directory.CreateDirectory(_tessDataPath);
                _logger.LogInformation("Created tessdata directory");
            }
            CheckLanguageFiles();
        }

        private void CheckLanguageFiles()
        {
            var amhPath = Path.Combine(_tessDataPath, "amh.traineddata");
            var engPath = Path.Combine(_tessDataPath, "eng.traineddata");
            
            _logger.LogInformation($"Amharic file exists: {File.Exists(amhPath)} at {amhPath}");
            _logger.LogInformation($"English file exists: {File.Exists(engPath)} at {engPath}");

            if (!File.Exists(amhPath))
            {
                _logger.LogWarning("Amharic language file not found. Amharic text extraction may not work properly.");
            }
            if (!File.Exists(engPath))
            {
                _logger.LogWarning("English language file not found. English text extraction may not work properly.");
            }
        }

        public async Task<OCRResult> ExtractTextFromImageAsync(string imageData)
        {
            return await ExtractTextFromImageAsync(imageData, "eng");
        }

        private async Task<OCRResult> ExtractTextFromImageAsync(string imageData, string languages)
        {
            try
            {
                _logger.LogInformation($"Starting OCR text extraction with languages: {languages}");
                
                imageData = CleanImageData(imageData);
                
                if (string.IsNullOrEmpty(imageData))
                {
                    return new OCRResult { Success = false, Error = "No image data provided" };
                }

                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(imageData);
                    _logger.LogInformation($"Image data length: {imageBytes.Length} bytes");
                }
                catch (FormatException ex)
                {
                    _logger.LogError(ex, "Invalid base64 image data");
                    return new OCRResult { Success = false, Error = "Invalid base64 image data" };
                }

                try
                {
                    using var engine = new TesseractEngine(_tessDataPath, languages, EngineMode.Default);
                    
                    // Optimized OCR settings for ID cards
                    engine.SetVariable("tessedit_pageseg_mode", "6");
                    engine.SetVariable("user_defined_dpi", "300");

                    using var img = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(img);
                    
                    var extractedText = page.GetText()?.Trim();
                    var confidence = page.GetMeanConfidence();

                    _logger.LogInformation($"OCR completed with confidence: {confidence}");
                    _logger.LogInformation($"Extracted text length: {extractedText?.Length ?? 0} characters");

                    // Fallback for low confidence
                    if (string.IsNullOrEmpty(extractedText) || confidence < 0.1)
                    {
                        _logger.LogInformation("Low confidence detected, trying alternative OCR settings...");
                        
                        using var engine2 = new TesseractEngine(_tessDataPath, languages, EngineMode.Default);
                        engine2.SetVariable("tessedit_pageseg_mode", "8");
                        
                        using var page2 = engine2.Process(img);
                        var altText = page2.GetText()?.Trim();
                        var altConfidence = page2.GetMeanConfidence();
                        
                        if (!string.IsNullOrEmpty(altText) && altConfidence > confidence)
                        {
                            extractedText = altText;
                            confidence = altConfidence;
                        }
                    }

                    return new OCRResult
                    {
                        Text = extractedText,
                        Confidence = confidence,
                        Success = !string.IsNullOrEmpty(extractedText) && confidence > 0.05
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tesseract engine error");
                    return new OCRResult { Success = false, Error = $"OCR engine error: {ex.Message}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OCR processing");
                return new OCRResult { Success = false, Error = $"OCR processing failed: {ex.Message}" };
            }
        }

        public async Task<EthiopianIDData> ProcessEthiopianIDAsync(string frontImageData, string backImageData)
        {
            var result = new EthiopianIDData();
            
            try
            {
                _logger.LogInformation("=== STARTING ETHIOPIAN ID PROCESSING ===");

                if (string.IsNullOrEmpty(frontImageData) && string.IsNullOrEmpty(backImageData))
                {
                    throw new Exception("Both front and back image data are empty.");
                }

                string frontText = "";
                string backText = "";

                // Process front image - Use English for better accuracy with names and FAN
                if (!string.IsNullOrEmpty(frontImageData))
                {
                    _logger.LogInformation("Processing front side of ID card...");
                    
                    var frontResult = await ExtractTextFromImageAsync(frontImageData, "eng");
                    
                    if (frontResult.Success && !string.IsNullOrEmpty(frontResult.Text))
                    {
                        frontText = frontResult.Text;
                        _logger.LogInformation($"Front side text extracted successfully. Length: {frontText.Length}");
                        _logger.LogInformation($"=== FRONT TEXT ===");
                        _logger.LogInformation(frontText);
                        _logger.LogInformation($"=== END FRONT TEXT ===");
                    }
                    else
                    {
                        _logger.LogWarning("OCR failed for front image");
                    }
                }

                // Process back image - Use both English and Amharic for better region detection
                if (!string.IsNullOrEmpty(backImageData))
                {
                    _logger.LogInformation("Processing back side of ID card...");
                    
                    // Try English + Amharic first for better region detection
                    var backResult = await ExtractTextFromImageAsync(backImageData, "eng+amh");
                    
                    if (!backResult.Success || string.IsNullOrEmpty(backResult.Text))
                    {
                        // Fallback to English only
                        backResult = await ExtractTextFromImageAsync(backImageData, "eng");
                    }
                    
                    if (backResult.Success && !string.IsNullOrEmpty(backResult.Text))
                    {
                        backText = backResult.Text;
                        _logger.LogInformation($"Back side text extracted successfully. Length: {backText.Length}");
                        _logger.LogInformation($"=== BACK TEXT ===");
                        _logger.LogInformation(backText);
                        _logger.LogInformation($"=== END BACK TEXT ===");
                    }
                    else
                    {
                        _logger.LogWarning("OCR failed for back image");
                    }
                }

                // Parse front image data
                if (!string.IsNullOrEmpty(frontText))
                {
                    ParseFrontImageData(frontText, result);
                }

                // Parse back image data
                if (!string.IsNullOrEmpty(backText))
                {
                    ParseBackImageData(backText, result);
                }

                // Set default values
                if (!string.IsNullOrEmpty(result.NationalId) || !string.IsNullOrEmpty(result.FirstName))
                {
                    result.Nationality = "Ethiopian";
                }

                // Calculate age based on the extracted date
                if (!string.IsNullOrEmpty(result.DateOfBirth))
                {
                    result.Age = CalculateAgeFromDateOfBirth(result.DateOfBirth, result.CalendarType);
                    _logger.LogInformation($"Calculated age using {result.CalendarType} calendar: {result.Age}");
                }

                // Final validation
                bool hasMinimalData = !string.IsNullOrEmpty(result.NationalId) || 
                                     !string.IsNullOrEmpty(result.FirstName) || 
                                     !string.IsNullOrEmpty(result.LastName);
                
                if (hasMinimalData)
                {
                    result.Success = true;
                    _logger.LogInformation($"✅ SUCCESS: Extracted valid ID information");
                }
                else
                {
                    result.Success = false;
                    result.Error = "Could not extract sufficient information from the ID card. Please ensure clear images and try again.";
                    _logger.LogWarning("❌ FAILED: Minimal information extracted from ID card");
                }

                _logger.LogInformation($"=== FINAL EXTRACTED DATA ===");
                _logger.LogInformation($"Success: {result.Success}");
                _logger.LogInformation($"FAN Number: {result.NationalId ?? "NOT FOUND"}");
                _logger.LogInformation($"FirstName: {result.FirstName ?? "NOT FOUND"}");
                _logger.LogInformation($"MiddleName: {result.MiddleName ?? "NOT FOUND"}");
                _logger.LogInformation($"LastName: {result.LastName ?? "NOT FOUND"}");
                _logger.LogInformation($"PhoneNumber: {result.PhoneNumber ?? "NOT FOUND"}");
                _logger.LogInformation($"Sex: {result.Sex ?? "NOT FOUND"}");
                _logger.LogInformation($"DateOfBirth: {result.DateOfBirth ?? "NOT FOUND"}");
                _logger.LogInformation($"CalendarType: {result.CalendarType ?? "NOT FOUND"}");
                _logger.LogInformation($"Age: {result.Age}");
                _logger.LogInformation($"Region: {result.Region ?? "NOT FOUND"}");

                return result;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Ethiopian ID");
                
                return new EthiopianIDData 
                { 
                    Success = false, 
                    Error = $"Could not extract information from ID card: {ex.Message}"
                };
            }
        }

        private void ParseFrontImageData(string text, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== PARSING FRONT IMAGE DATA ===");
                
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                               .Select(line => line.Trim())
                               .Where(line => !string.IsNullOrEmpty(line))
                               .ToArray();

                _logger.LogInformation($"Found {lines.Length} lines in front image");

                // EXTRACT NAMES - Focus on English text patterns
                ExtractNamesFromFront(text, lines, data);

                // EXTRACT FAN NUMBER - Exact number extraction
                ExtractFANFromFront(text, lines, data);

                // EXTRACT GENDER
                ExtractGenderFromFront(text, data);

                // EXTRACT DATE OF BIRTH
                ExtractDateOfBirthFromFront(text, lines, data);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing front image data");
            }
        }

        private void ParseBackImageData(string text, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== PARSING BACK IMAGE DATA ===");
                
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                               .Select(line => line.Trim())
                               .Where(line => !string.IsNullOrEmpty(line))
                               .ToArray();

                _logger.LogInformation($"Found {lines.Length} lines in back image");

                // EXTRACT PHONE NUMBER
                ExtractPhoneNumberFromBack(text, lines, data);

                // EXTRACT REGION - ENHANCED DETECTION
                ExtractRegionFromBack(text, lines, data);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing back image data");
            }
        }

        private void ExtractNamesFromFront(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== EXTRACTING NAMES FROM FRONT ===");

                // Method 1: Look for English name patterns (3 words, proper capitalization)
                foreach (var line in lines)
                {
                    var cleanLine = line.Trim();
                    _logger.LogInformation($"Checking line: '{cleanLine}'");

                    // Look for lines that contain typical English name patterns
                    if (cleanLine.Length >= 5 && cleanLine.Length <= 50)
                    {
                        var words = cleanLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        
                        // Check if this looks like an English name (2-4 parts, proper capitalization)
                        if (words.Length >= 2 && words.Length <= 4)
                        {
                            bool looksLikeEnglishName = true;
                            foreach (var word in words)
                            {
                                // English names should start with capital letter and contain only letters
                                if (word.Length < 2 || 
                                    !char.IsUpper(word[0]) || 
                                    word.Any(char.IsDigit) || 
                                    word.ToUpper().Contains("FAN") || 
                                    word.ToUpper().Contains("ID") ||
                                    word.ToUpper().Contains("DATE") || 
                                    word.ToUpper().Contains("BIRTH") ||
                                    word.ToUpper().Contains("SEX") ||
                                    word.ToUpper().Contains("GENDER"))
                                {
                                    looksLikeEnglishName = false;
                                    break;
                                }
                            }

                            if (looksLikeEnglishName)
                            {
                                _logger.LogInformation($"Found potential English name: '{cleanLine}'");
                                
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
                                
                                _logger.LogInformation($"✅ EXTRACTED ENGLISH NAMES: {data.FirstName} {data.MiddleName} {data.LastName}");
                                return;
                            }
                        }
                    }
                }

                // Method 2: Look for "Name" or "Full Name" label with English text
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].ToLower().Contains("name") || lines[i].ToLower().Contains("full name"))
                    {
                        _logger.LogInformation($"Found name label at line {i}: {lines[i]}");
                        
                        // Check next 2 lines for English name
                        for (int j = i + 1; j <= i + 2 && j < lines.Length; j++)
                        {
                            var nameLine = lines[j].Trim();
                            if (IsValidEnglishNameLine(nameLine))
                            {
                                ParseEnglishNameComponents(nameLine, data);
                                _logger.LogInformation($"✅ EXTRACTED ENGLISH NAME FROM LABEL: {data.FirstName} {data.MiddleName} {data.LastName}");
                                return;
                            }
                        }
                    }
                }

                _logger.LogInformation("❌ No English names found in front image");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting names from front");
            }
        }

        private void ExtractFANFromFront(string text, string[] lines, EthiopianIDData data)
{
    try
    {
        _logger.LogInformation("=== ENHANCED FAN EXTRACTION FROM FRONT ===");

        // COMPREHENSIVE FAN PATTERNS - COVERS ALL ETHIOPIAN ID FORMATS
        var fanPatterns = new[]
        {
            // Standard Ethiopian FAN formats
            @"(ETA|ETB|ETC|ETD)\s*(\d{7})", // ETA1234567
            @"FAN\s*[:]?\s*([A-Z0-9]{10,17})", // FAN: 12345678901234567
            @"FAN\s*NO\s*[:]?\s*([A-Z0-9]{10,17})", // FAN NO: 12345678901234567
            @"ID\s*NO\s*[:]?\s*([A-Z0-9]{10,17})", // ID NO: 12345678901234567
            @"ID\s*[:]?\s*([A-Z0-9]{10,17})", // ID: 12345678901234567
            
            // Number sequences (pure digits)
            @"\b\d{10,17}\b", // 10-17 digit number
            @"\b\d{4}\s*\d{4}\s*\d{4}\s*\d{3,5}\b", // 4-4-4-3/4/5 pattern
            @"\b\d{3}\s*\d{3}\s*\d{3}\s*\d{3}\b", // 3-3-3-3 pattern
            
            // OCR error tolerant patterns
            @"(ET[ABCD])\s*(\d+)", // ET followed by A/B/C/D and numbers
            @"(FAN|ID)\s*[:]?\s*([0-9OIl]{10,17})", // Tolerant of OCR errors
        };

        foreach (var pattern in fanPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string fanCandidate = "";
                
                if (match.Groups.Count > 2)
                {
                    // Pattern with prefix and numbers (like ETA1234567)
                    fanCandidate = match.Groups[1].Value + match.Groups[2].Value;
                }
                else if (match.Groups.Count > 1)
                {
                    // Pattern with captured group
                    fanCandidate = match.Groups[1].Value;
                }
                else
                {
                    // Direct match
                    fanCandidate = match.Value;
                }

                // Clean and validate the FAN candidate
                fanCandidate = CleanFANNumber(fanCandidate);
                
                if (IsValidFANNumber(fanCandidate))
                {
                    data.NationalId = fanCandidate;
                    _logger.LogInformation($"✅✅✅ FOUND VALID FAN: {data.NationalId} (pattern: {pattern})");
                    return;
                }
            }
        }

        // FALLBACK: Line-by-line analysis for FAN
        foreach (var line in lines)
        {
            var upperLine = line.ToUpper();
            if (upperLine.Contains("FAN") || upperLine.Contains("ID NO") || upperLine.Contains("ID:"))
            {
                _logger.LogInformation($"🔍 Checking FAN line: {line}");
                
                // Extract all potential FAN sequences
                var potentialFans = Regex.Matches(line, @"[A-Z0-9]{10,17}");
                foreach (Match potential in potentialFans)
                {
                    var cleanFan = CleanFANNumber(potential.Value);
                    if (IsValidFANNumber(cleanFan))
                    {
                        data.NationalId = cleanFan;
                        _logger.LogInformation($"✅ FOUND FAN IN LABEL LINE: {data.NationalId}");
                        return;
                    }
                }
            }
        }

        _logger.LogWarning("❌ No valid FAN number found after comprehensive search");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in enhanced FAN extraction from front");
    }
}

// ADD THESE HELPER METHODS TO TesseractOCRService.cs
private string CleanFANNumber(string fan)
{
    if (string.IsNullOrEmpty(fan)) return fan;

    // Remove spaces and common separators
    fan = Regex.Replace(fan, @"[\s\-\.]", "");
    
    // Fix common OCR errors
    fan = fan.Replace("O", "0")  // Letter O to zero
            .Replace("o", "0")   // Lowercase o to zero
            .Replace("I", "1")   // Capital I to one
            .Replace("l", "1")   // Lowercase L to one
            .Replace("S", "5")   // S to five
            .Replace("B", "8")   // B to eight
            .Replace("Z", "2");  // Z to two

    return fan.ToUpper();
}

private bool IsValidFANNumber(string fan)
{
    if (string.IsNullOrEmpty(fan)) return false;

    // Ethiopian FAN numbers are typically 10-17 alphanumeric characters
    if (fan.Length < 10 || fan.Length > 17) return false;

    // Must be alphanumeric
    if (!fan.All(c => char.IsLetterOrDigit(c))) return false;

    // If it starts with ET, it should be ETA/ETB/ETC/ETD
    if (fan.StartsWith("ET") && fan.Length >= 3)
    {
        char thirdChar = fan[2];
        if (thirdChar != 'A' && thirdChar != 'B' && thirdChar != 'C' && thirdChar != 'D')
            return false;
    }

    _logger.LogInformation($"✅ Valid FAN candidate: {fan}");
    return true;
}

        private void ExtractPhoneNumberFromBack(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== EXTRACTING PHONE NUMBER FROM BACK ===");

                // Look for Ethiopian phone number patterns
                var phonePatterns = new[]
                {
                    @"(+251\d{9})",
                    @"(251\d{9})",
                    @"(09\d{8})",
                    @"(9\d{8})"
                };

                foreach (var pattern in phonePatterns)
                {
                    var matches = Regex.Matches(text, pattern);
                    foreach (Match match in matches)
                    {
                        var phone = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                        if (!string.IsNullOrEmpty(phone))
                        {
                            data.PhoneNumber = FormatPhoneNumber(phone);
                            _logger.LogInformation($"✅ FOUND PHONE: {data.PhoneNumber}");
                            return;
                        }
                    }
                }

                _logger.LogInformation("❌ No phone number found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting phone number from back");
            }
        }

        private void ExtractGenderFromFront(string text, EthiopianIDData data)
        {
            try
            {
                var genderPatterns = new[]
                {
                    @"Sex[:]?\s*(\w+)",
                    @"Gender[:]?\s*(\w+)",
                    @"(Male|Female)",
                    @"(M|F)\b"
                };

                foreach (var pattern in genderPatterns)
                {
                    var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var genderValue = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
                        
                        if (genderValue.ToUpper().Contains("MALE") || genderValue.ToUpper() == "M")
                        {
                            data.Sex = "Male";
                            _logger.LogInformation($"✅ FOUND GENDER: Male");
                            return;
                        }
                        else if (genderValue.ToUpper().Contains("FEMALE") || genderValue.ToUpper() == "F")
                        {
                            data.Sex = "Female";
                            _logger.LogInformation($"✅ FOUND GENDER: Female");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting gender");
            }
        }

        private void ExtractDateOfBirthFromFront(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== EXTRACTING DATE OF BIRTH FROM FRONT ===");

                // Look for the "Date of Birth" line specifically
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].ToLower().Contains("date of birth") || lines[i].ToLower().Contains("birth date"))
                    {
                        _logger.LogInformation($"Found date of birth label at line {i}: {lines[i]}");
                        
                        // Check current line and next 2 lines for date patterns
                        for (int j = i; j <= i + 2 && j < lines.Length; j++)
                        {
                            if (ExtractBirthDateFromLine(lines[j], data))
                            {
                                _logger.LogInformation($"✅ EXTRACTED DATE OF BIRTH: {data.DateOfBirth} ({data.CalendarType})");
                                return;
                            }
                        }
                    }
                }

                // Fallback: Search entire text for date patterns
                if (ExtractBirthDateFromLine(text, data))
                {
                    _logger.LogInformation($"✅ EXTRACTED DATE OF BIRTH FROM TEXT: {data.DateOfBirth} ({data.CalendarType})");
                    return;
                }

                _logger.LogInformation("❌ No date of birth found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting date of birth");
            }
        }

        private bool ExtractBirthDateFromLine(string line, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation($"Extracting birth date from line: {line}");

                // Look for Ethiopian date format (dd/mm/yyyy) | Gregorian date (yyyy/mmm/dd)
                var birthDatePattern = @"(\d{1,2}/\d{1,2}/\d{4})\s*[|]\s*(\d{4}/[A-Za-z]{3}/\d{1,2})";
                var birthDateMatch = Regex.Match(line, birthDatePattern, RegexOptions.IgnoreCase);
                
                if (birthDateMatch.Success && birthDateMatch.Groups.Count >= 3)
                {
                    var ethiopianDate = birthDateMatch.Groups[1].Value.Trim();
                    data.DateOfBirth = ethiopianDate;
                    data.CalendarType = "Ethiopian";
                    _logger.LogInformation($"✅ SELECTED ETHIOPIAN DATE OF BIRTH: {data.DateOfBirth}");
                    return true;
                }

                // Look for standalone Ethiopian date (dd/mm/yyyy)
                var ethiopianPattern = @"\b(\d{1,2}/\d{1,2}/\d{4})\b";
                var ethiopianMatch = Regex.Match(line, ethiopianPattern);
                if (ethiopianMatch.Success)
                {
                    data.DateOfBirth = ethiopianMatch.Groups[1].Value.Trim();
                    data.CalendarType = "Ethiopian";
                    _logger.LogInformation($"✅ FOUND ETHIOPIAN DATE: {data.DateOfBirth}");
                    return true;
                }

                // Look for standalone Gregorian date
                var gregorianPattern = @"\b(\d{4}/[A-Za-z]{3}/\d{1,2})\b";
                var gregorianMatch = Regex.Match(line, gregorianPattern, RegexOptions.IgnoreCase);
                if (gregorianMatch.Success)
                {
                    data.DateOfBirth = gregorianMatch.Groups[1].Value.Trim();
                    data.CalendarType = "Gregorian";
                    _logger.LogInformation($"✅ FOUND GREGORIAN DATE: {data.DateOfBirth}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting birth date from line");
                return false;
            }
        }

        // ========== ENHANCED REGION EXTRACTION METHODS ==========

        private void ExtractRegionFromBack(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== ENHANCED REGION EXTRACTION FROM BACK IMAGE ===");
                _logger.LogInformation($"Text length: {text.Length} characters");
                _logger.LogInformation($"Number of lines: {lines.Length}");

                // Log first 10 lines for debugging
                _logger.LogInformation("=== FIRST 10 LINES OF BACK IMAGE ===");
                for (int i = 0; i < Math.Min(10, lines.Length); i++)
                {
                    _logger.LogInformation($"Line {i}: '{lines[i]}'");
                }
                _logger.LogInformation("=== END LINES ===");

                // STRATEGY 1: Enhanced Amhara detection with extensive patterns
                if (FindAmharaRegionWithEnhancedPatterns(text, lines, data))
                {
                    return;
                }

                // STRATEGY 2: Look for region below address/አድራሻ text (Primary Strategy)
                if (ExtractRegionFromAddressSectionEnhanced(lines, data))
                {
                    return;
                }

                // STRATEGY 3: Administrative hierarchy detection
                if (ExtractRegionFromAdminHierarchy(lines, data))
                {
                    return;
                }

                // STRATEGY 4: Look for other Ethiopian regions
                if (FindOtherRegionsEnhanced(text, data))
                {
                    return;
                }

                // STRATEGY 5: Direct text search for any region patterns
                if (ExtractRegionWithComprehensiveMatching(text, data))
                {
                    return;
                }

                _logger.LogWarning("❌ No region found in back image after all strategies");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting region from back");
            }
        }

        private bool FindAmharaRegionWithEnhancedPatterns(string text, string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== ENHANCED AMHARA REGION DETECTION ===");

                // EXTENSIVE Amhara patterns including OCR variations and Amharic text
                var amharaPatterns = new[]
                {
                    // English variations
                    "Amhara", "Amara", "Amhra", "Amahr", "Amhar", "Amhora", "Amahra", "Anhara",
                    "AMHARA", "amhara", "Amharra", "Anhara", "Amhara Region", "Amhara National",
                    "Amhara State", "Amhara Regional", "Amhara R/gional State", "Amhara Rgional State",
                    "Amhara Regonal State", "Amhara Reginal State", "Amhara Regianl State",
                    
                    // Amharic text variations (አማራ)
                    "አማራ", "አማራ ክልል", "አማራ ሬጅን", "አማራ ሬጅናል", "አማራ ክልል ሬጅናል",
                    
                    // Common OCR errors for Amharic text
                    "አማር", "አማራ፣", "አማራ፤", "አማራ:", "አማራ-", "አምራ", "አማረ",
                    
                    // Additional variations
                    "Amhara Regional State", "Amhara R. State", "Amhara R State",
                    "Amhara Zone", "Amhara Administrative", "Amhara National Regional State",
                    "Amhara R/S", "Amhara R/Sate", "Amhara R. S.", "Amhara Reg. State"
                };

                // Check each pattern in the entire text
                foreach (var pattern in amharaPatterns)
                {
                    if (text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        data.Region = "Amhara";
                        _logger.LogInformation($"✅ FOUND AMHARA REGION: Matched pattern '{pattern}'");
                        return true;
                    }
                }

                // Check each line individually (more precise)
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    foreach (var pattern in amharaPatterns)
                    {
                        if (line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            data.Region = "Amhara";
                            _logger.LogInformation($"✅ FOUND AMHARA REGION IN LINE {i}: '{line}'");
                            return true;
                        }
                    }
                }

                // Enhanced partial matching with context awareness
                if (text.IndexOf("Amhar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Amara", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Amhra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("አማራ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("አማር", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    data.Region = "Amhara";
                    _logger.LogInformation($"✅ FOUND AMHARA REGION VIA ENHANCED PARTIAL MATCH");
                    return true;
                }

                // Check for Amhara in context with other regional keywords
                if (ContainsAmharaInContext(text))
                {
                    data.Region = "Amhara";
                    _logger.LogInformation($"✅ FOUND AMHARA REGION VIA CONTEXT ANALYSIS");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding Amhara region");
                return false;
            }
        }

        private bool ExtractRegionFromAddressSectionEnhanced(string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== ENHANCED ADDRESS SECTION DETECTION ===");

                // Amharic and English address keywords - focused on back image patterns
                var addressKeywords = new[]
                {
                    "Address", "Location", "Area", "Region", "Kifle Ketema",
                    "አድራሻ", "ቦታ", "ክልል", "ከተማ", "መኖሪያ", "የተማሪ",
                    "Adrasha", "Adrash", "Adrara", "Adr", "Addr", "Addresh", // Common OCR variations
                    "ያገር", "የት", "Where", "Residential", "Domicile", "Place of Residence",
                    "Place", "Residence", "Student", "Resident"
                };

                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var keyword in addressKeywords)
                    {
                        if (lines[i].IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _logger.LogInformation($"✅ Found address keyword '{keyword}' at line {i}: '{lines[i]}'");

                            // Enhanced: Look for region in the next 3-5 lines (typical address structure on back of ID)
                            for (int j = i + 1; j <= i + 5 && j < lines.Length; j++)
                            {
                                _logger.LogInformation($"🔍 Checking line {j} below address: '{lines[j]}'");
                                
                                var region = ExtractRegionWithEnhancedMatching(lines[j]);
                                if (!string.IsNullOrEmpty(region))
                                {
                                    data.Region = region;
                                    _logger.LogInformation($"✅✅✅ FOUND REGION BELOW ADDRESS LABEL: {data.Region} at line {j}");
                                    return true;
                                }
                            }

                            // Also check the same line after the keyword
                            var textAfterKeyword = GetTextAfterLabel(lines[i], keyword);
                            if (!string.IsNullOrEmpty(textAfterKeyword))
                            {
                                var region = ExtractRegionWithEnhancedMatching(textAfterKeyword);
                                if (!string.IsNullOrEmpty(region))
                                {
                                    data.Region = region;
                                    _logger.LogInformation($"✅✅✅ FOUND REGION IN SAME LINE AFTER LABEL: {data.Region}");
                                    return true;
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("❌ No region found below address labels");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced address section detection");
                return false;
            }
        }

        private bool ExtractRegionFromAdminHierarchy(string[] lines, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== EXTRACTING REGION FROM ADMIN HIERARCHY ===");

                // Ethiopian administrative hierarchy: Region -> Zone -> Woreda -> Kebele
                var adminIndicators = new[]
                {
                    "Zone", "Woreda", "Kebele", "Worda", "Sub-city", "Kifle Ketema",
                    "ዞን", "ወረዳ", "ቀበሌ", "ክፍለ ከተማ", "Zone:", "Woreda:", "Kebele:"
                };

                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (var indicator in adminIndicators)
                    {
                        if (lines[i].Contains(indicator, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation($"Found admin indicator '{indicator}' at line {i}: '{lines[i]}'");

                            // Region typically comes before zone/woreda in the hierarchy
                            // Look backwards 1-4 lines for region
                            for (int j = i - 1; j >= Math.Max(0, i - 4); j--)
                            {
                                var region = FindRegionInText(lines[j]);
                                if (region != null)
                                {
                                    data.Region = region;
                                    _logger.LogInformation($"✅ FOUND REGION ABOVE ADMIN INDICATOR: {data.Region}");
                                    return true;
                                }
                            }

                            // Also check current line
                            var regionInLine = FindRegionInText(lines[i]);
                            if (regionInLine != null)
                            {
                                data.Region = regionInLine;
                                _logger.LogInformation($"✅ FOUND REGION IN SAME LINE AS ADMIN INDICATOR: {data.Region}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting region from admin hierarchy");
                return false;
            }
        }

        private bool FindOtherRegionsEnhanced(string text, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== LOOKING FOR OTHER ETHIOPIAN REGIONS ===");

                var ethiopianRegions = new Dictionary<string, string[]>
                {
                    ["Oromia"] = new[] { "Oromia", "Oromiya", "Oromiyaa", "Oromya", "Oromiia", "Oromi", "ኦሮሚያ" },
                    ["Tigray"] = new[] { "Tigray", "Tigrai", "Tigra", "Tigray Region", "ትግራይ" },
                    ["SNNPR"] = new[] { "SNNPR", "SNNP", "Southern Nations", "Southern Nations Nationalities", "ደቡብ" },
                    ["Addis Ababa"] = new[] { "Addis Ababa", "Addis", "Adis Ababa", "Adis", "Addis Abeba", "አዲስ አበባ" },
                    ["Somali"] = new[] { "Somali", "Somale", "Somal", "Somali Region", "ሶማሌ" },
                    ["Afar"] = new[] { "Afar", "Affar", "Afr", "Afar Region", "አፋር" },
                    ["Dire Dawa"] = new[] { "Dire Dawa", "Dire-Dawa", "Diredawa", "ድሬ ዳዋ" },
                    ["Benishangul-Gumuz"] = new[] { "Benishangul", "Benishangul-Gumuz", "Benshangul", "Benshangul-Gumuz", "ቤንሻንጉል" },
                    ["Gambela"] = new[] { "Gambela", "Gambella", "Gambela Region", "ጋምቤላ" },
                    ["Harari"] = new[] { "Harari", "Harar", "Harari Region", "ሐረሪ" }
                };

                foreach (var region in ethiopianRegions)
                {
                    foreach (var pattern in region.Value)
                    {
                        if (text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            data.Region = region.Key;
                            _logger.LogInformation($"✅ FOUND REGION: {region.Key} (matched pattern: {pattern})");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding other regions");
                return false;
            }
        }

        private bool ExtractRegionWithComprehensiveMatching(string text, EthiopianIDData data)
        {
            try
            {
                _logger.LogInformation("=== COMPREHENSIVE REGION MATCHING ===");

                var regionPatterns = new Dictionary<string, string[]>
                {
                    ["Amhara"] = new[]
                    {
                        "Amhara", "Amara", "Amhra", "Amahr", "Amhar", "Amhora", "Amahra", "Anhara",
                        "AMHARA", "amhara", "Amharra", "Anhara", "Amhara Region", "Amhara National",
                        "አማራ", "አማራ ክልል", "አማራ ሬጅን", "አማራ ሬጅናል", "አማር"
                    },
                    ["Oromia"] = new[]
                    {
                        "Oromia", "Oromiya", "Oromiyaa", "Oromya", "Oromiia", "Oromi", "ኦሮሚያ", "Oromiya Region",
                        "OROMIA", "oromia", "Oromiyaa", "Oromiya Regional", "Oromiya Regional State", "Oromia Regional State"
                    },
                    ["Tigray"] = new[]
                    {
                        "Tigray", "Tigrai", "Tigray Region", "Tigray State", "Tigray Regional",
                        "TIGRAY", "tigray", "Tigra", "Tigrai Region", "Tigray Regional State", "ትግራይ"
                    },
                    ["SNNPR"] = new[]
                    {
                        "SNNPR", "SNNP", "Southern Nations", "Southern Nations Nationalities",
                        "Southern Nations, Nationalities", "SNNPRS", "Snnpr", "snnpr",
                        "Southern Nations Nationalities and Peoples", "SNNPR Region", "ደቡብ"
                    },
                    ["Addis Ababa"] = new[]
                    {
                        "Addis Ababa", "Addis", "Adis Ababa", "Adis", "Addis Abeba",
                        "ADDIS ABABA", "Addis Ababa City", "Finfine", "Addis Ababa City Administration", "አዲስ አበባ"
                    },
                    ["Somali"] = new[]
                    {
                        "Somali", "Somale", "Somali Region", "Somali State", "Somali Regional",
                        "SOMALI", "somali", "Somal", "Somali Regional State", "ሶማሌ"
                    },
                    ["Afar"] = new[]
                    {
                        "Afar", "Affar", "Afar Region", "Afar State", "Afar Regional",
                        "AFAR", "afar", "Afr", "Afar Regional State", "አፋር"
                    },
                    ["Dire Dawa"] = new[]
                    {
                        "Dire Dawa", "Dire-Dawa", "Dire Dawa City", "Dire Dawa Administration",
                        "DIRE DAWA", "dire dawa", "Diredawa", "Dire Dawa City Administration", "ድሬ ዳዋ"
                    },
                    ["Benishangul-Gumuz"] = new[]
                    {
                        "Benishangul", "Benishangul-Gumuz", "Benshangul", "Benshangul-Gumuz",
                        "Benishangul Gumuz", "Benishangul Gumuz Region", "Benishangul Regional State", "ቤንሻንጉል"
                    },
                    ["Gambela"] = new[]
                    {
                        "Gambela", "Gambella", "Gambela Region", "Gambella Regional State",
                        "GAMBELA", "gambela", "Gambella Region", "Gambela Peoples", "Gambela Regional", "ጋምቤላ"
                    },
                    ["Harari"] = new[]
                    {
                        "Harari", "Harari Region", "Harari People", "Harari Regional State",
                        "HARARI", "harari", "Harar", "Harari People Regional State", "Harari Regional", "ሐረሪ"
                    }
                };

                foreach (var region in regionPatterns)
                {
                    foreach (var pattern in region.Value)
                    {
                        if (text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            data.Region = region.Key;
                            _logger.LogInformation($"✅ FOUND REGION WITH COMPREHENSIVE MATCHING: {data.Region} (pattern: {pattern})");
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive region matching");
                return false;
            }
        }

        private string ExtractRegionWithEnhancedMatching(string line)
        {
            try
            {
                if (string.IsNullOrEmpty(line)) return null;

                // Enhanced Amhara detection with extensive OCR variations
                var amharaPatterns = new[]
                {
                    "Amhara", "Amara", "Amhra", "Amahr", "Amhar", "Amhora", "Amahra", "Anhara",
                    "AMHARA", "amhara", "Amharra", "Anhara", "Amhara Region", "Amhara National",
                    "አማራ", "አማራ ክልል", "አማራ ሬጅን", "አማራ ሬጅናል", "አማር", "አማራ፣", "አማራ፤", "አማራ:"
                };

                foreach (var pattern in amharaPatterns)
                {
                    if (line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _logger.LogInformation($"🎯 Matched Amhara pattern: '{pattern}' in line: '{line}'");
                        return "Amhara";
                    }
                }

                // Other Ethiopian regions with enhanced patterns
                var otherRegions = new Dictionary<string, string[]>
                {
                    ["Oromia"] = new[] { "Oromia", "Oromiya", "Oromiyaa", "Oromya", "Oromiia", "Oromi", "ኦሮሚያ", "Oromiya Region" },
                    ["Tigray"] = new[] { "Tigray", "Tigrai", "Tigra", "Tigray Region", "ትግራይ", "Tigray Regional State" },
                    ["SNNPR"] = new[] { "SNNPR", "SNNP", "Southern Nations", "Southern Nations Nationalities", "ደቡብ", "SNNPR Region" },
                    ["Addis Ababa"] = new[] { "Addis Ababa", "Addis", "Adis Ababa", "Adis", "Addis Abeba", "አዲስ አበባ", "Addis Ababa City" },
                    ["Somali"] = new[] { "Somali", "Somale", "Somal", "Somali Region", "ሶማሌ", "Somali Regional State" },
                    ["Afar"] = new[] { "Afar", "Affar", "Afr", "Afar Region", "አፋር", "Afar Regional State" },
                    ["Dire Dawa"] = new[] { "Dire Dawa", "Dire-Dawa", "Diredawa", "ድሬ ዳዋ", "Dire Dawa City" },
                    ["Benishangul-Gumuz"] = new[] { "Benishangul", "Benishangul-Gumuz", "Benshangul", "Benshangul-Gumuz", "ቤንሻንጉል", "Benishangul Gumuz" },
                    ["Gambela"] = new[] { "Gambela", "Gambella", "Gambela Region", "ጋምቤላ", "Gambella Regional State" },
                    ["Harari"] = new[] { "Harari", "Harar", "Harari Region", "ሐረሪ", "Harari Regional State" }
                };

                foreach (var region in otherRegions)
                {
                    foreach (var pattern in region.Value)
                    {
                        if (line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _logger.LogInformation($"🎯 Matched {region.Key} pattern: '{pattern}' in line: '{line}'");
                            return region.Key;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in enhanced region matching");
                return null;
            }
        }

        private bool ContainsAmharaInContext(string text)
        {
            try
            {
                // Look for Amhara in combination with other regional indicators
                var contextPatterns = new[]
                {
                    @"(Amhara|Amara|አማራ).*(Region|Regional|State|ክልል|ዞን)",
                    @"(Region|Regional|State|ክልል).*(Amhara|Amara|አማራ)",
                    @"(Address|Location|Area).*(Amhara|Amara|አማራ)",
                    @"(Amhara|Amara|አማራ).*(Address|Location|Area)"
                };

                foreach (var pattern in contextPatterns)
                {
                    if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in context analysis for Amhara");
                return false;
            }
        }

        // ========== HELPER METHODS FOR REGION EXTRACTION ==========

        private bool IsAddressSectionStart(string line)
        {
            var addressKeywords = new[]
            {
                "Address", "Location", "Area", "Region", "Kifle Ketema", 
                "ክልል", "የትምህርት ክልል", "Place", "Residence", "የተማሪ",
                "Student", "Resident", "መኖሪያ", "የት", "Where", "Residential"
            };

            foreach (var keyword in addressKeywords)
            {
                if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsRegion(string text, string region)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(region))
                return false;

            // Exact match (case insensitive)
            if (text.Contains(region, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check for common OCR errors for this specific region
            var variations = GetRegionVariations(region);
            foreach (var variation in variations)
            {
                if (text.Contains(variation, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private string DetectRegionWithVariations(string text)
        {
            var regionVariations = new Dictionary<string, string[]>
            {
                ["Amhara"] = new[] { "Amhara", "Amara", "Amhra", "Amahr", "Amhar", "Amhora", "Amahra", "Anhara" },
                ["Oromia"] = new[] { "Oromia", "Oromiya", "Oromiyaa", "Oromya", "Oromiia", "Oromi" },
                ["Tigray"] = new[] { "Tigray", "Tigrai", "Tigra" },
                ["SNNPR"] = new[] { "SNNPR", "SNNP", "Southern Nations" },
                ["Addis Ababa"] = new[] { "Addis Ababa", "Addis", "Adis Ababa", "Adis", "Addis Abeba" },
                ["Somali"] = new[] { "Somali", "Somale", "Somal" },
                ["Afar"] = new[] { "Afar", "Affar", "Afr" },
                ["Dire Dawa"] = new[] { "Dire Dawa", "Dire-Dawa", "Diredawa" },
                ["Benishangul"] = new[] { "Benishangul", "Benishangul-Gumuz", "Benshangul", "Benshangul-Gumuz" },
                ["Gambela"] = new[] { "Gambela", "Gambella" },
                ["Harari"] = new[] { "Harari", "Harar" }
            };

            foreach (var region in regionVariations)
            {
                foreach (var variation in region.Value)
                {
                    if (text.Contains(variation, StringComparison.OrdinalIgnoreCase))
                    {
                        return region.Key;
                    }
                }
            }

            return null;
        }

        private string[] GetRegionVariations(string region)
        {
            var variations = new Dictionary<string, string[]>
            {
                ["Amhara"] = new[] { "Amhara", "Amara", "Amhra", "Amahr", "Amhar", "Amhora", "Amahra", "Anhara" },
                ["Oromia"] = new[] { "Oromia", "Oromiya", "Oromiyaa", "Oromya", "Oromiia", "Oromi" },
                ["Tigray"] = new[] { "Tigray", "Tigrai", "Tigra" },
                ["SNNPR"] = new[] { "SNNPR", "SNNP", "Southern Nations" },
                ["Addis Ababa"] = new[] { "Addis Ababa", "Addis", "Adis Ababa", "Adis", "Addis Abeba" },
                ["Somali"] = new[] { "Somali", "Somale", "Somal" },
                ["Afar"] = new[] { "Afar", "Affar", "Afr" },
                ["Dire Dawa"] = new[] { "Dire Dawa", "Dire-Dawa", "Diredawa" },
                ["Benishangul"] = new[] { "Benishangul", "Benishangul-Gumuz", "Benshangul", "Benshangul-Gumuz" },
                ["Gambela"] = new[] { "Gambela", "Gambella" },
                ["Harari"] = new[] { "Harari", "Harar" }
            };

            return variations.ContainsKey(region) ? variations[region] : new[] { region };
        }

        private string FindRegionInText(string text)
        {
            var ethiopianRegions = new[]
            {
                "Amhara", "Oromia", "Tigray", "SNNPR", "Addis Ababa", 
                "Somali", "Afar", "Dire Dawa", "Benishangul", "Gambela", "Harari"
            };

            foreach (var region in ethiopianRegions)
            {
                if (ContainsRegion(text, region))
                {
                    return region;
                }
            }
            return null;
        }

        private string GetTextAfterLabel(string line, string label)
        {
            var index = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return line.Substring(index + label.Length).Trim();
            }
            return null;
        }

        // ========== UTILITY METHODS ==========

        private void ParseEnglishNameComponents(string fullName, EthiopianIDData data)
        {
            try
            {
                fullName = Regex.Replace(fullName, @"[^\w\s]", "").Trim();
                var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                _logger.LogInformation($"English name parts: {string.Join("|", nameParts)}");

                if (nameParts.Length >= 3)
                {
                    data.FirstName = nameParts[0];
                    data.MiddleName = nameParts[1];
                    data.LastName = string.Join(" ", nameParts.Skip(2));
                }
                else if (nameParts.Length == 2)
                {
                    data.FirstName = nameParts[0];
                    data.LastName = nameParts[1];
                }
                else if (nameParts.Length == 1)
                {
                    data.FirstName = nameParts[0];
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing English name components");
            }
        }

        private bool IsValidEnglishNameLine(string line)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 3)
                return false;

            // Check if line contains digits or ID-related keywords
            if (Regex.IsMatch(line, @"\d") ||
                line.ToUpper().Contains("FAN") ||
                line.ToUpper().Contains("ID") ||
                line.ToUpper().Contains("DATE") ||
                line.ToUpper().Contains("BIRTH") ||
                line.ToUpper().Contains("SEX") ||
                line.ToUpper().Contains("GENDER"))
                return false;

            // Check if line has proper English name capitalization
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words.Length > 4)
                return false;

            // Check if each word starts with capital letter
            foreach (var word in words)
            {
                if (word.Length < 2 || !char.IsUpper(word[0]))
                    return false;
            }

            return true;
        }

        private string FormatPhoneNumber(string phoneNumber)
        {
            try
            {
                var digits = Regex.Replace(phoneNumber, @"[^\d]", "");
                
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
                
                return phoneNumber;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting phone number");
                return phoneNumber;
            }
        }

        private int CalculateAgeFromDateOfBirth(string dateOfBirth, string calendarType)
        {
            try
            {
                _logger.LogInformation($"Calculating age from: {dateOfBirth} using {calendarType} calendar");

                if (string.IsNullOrEmpty(dateOfBirth))
                {
                    _logger.LogWarning("Date of birth is empty");
                    return 0;
                }

                if (calendarType == "Gregorian")
                {
                    return CalculateAgeFromGregorianDate(dateOfBirth);
                }
                else if (calendarType == "Ethiopian")
                {
                    return CalculateAgeFromEthiopianDate(dateOfBirth);
                }
                else
                {
                    _logger.LogWarning($"Unknown calendar type: {calendarType}");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating age from date of birth");
                return 0;
            }
        }

        private int CalculateAgeFromGregorianDate(string dateOfBirth)
        {
            try
            {
                _logger.LogInformation($"Calculating age from Gregorian date: {dateOfBirth}");

                DateTime birthDate = DateTime.MinValue;
                bool parseSuccess = false;

                // Try to parse as yyyy/MMM/dd format
                var yearMonthNameDayPattern = @"\b(\d{4})[/-]([A-Za-z]{3,})[/-](\d{1,2})\b";
                var yearMonthNameDayMatch = Regex.Match(dateOfBirth, yearMonthNameDayPattern, RegexOptions.IgnoreCase);
                
                if (yearMonthNameDayMatch.Success)
                {
                    var year = int.Parse(yearMonthNameDayMatch.Groups[1].Value);
                    var monthName = yearMonthNameDayMatch.Groups[2].Value.ToLower();
                    var day = int.Parse(yearMonthNameDayMatch.Groups[3].Value);

                    int month = monthName switch
                    {
                        "jan" => 1, "feb" => 2, "mar" => 3, "apr" => 4, "may" => 5, "jun" => 6,
                        "jul" => 7, "aug" => 8, "sep" => 9, "oct" => 10, "nov" => 11, "dec" => 12,
                        _ => 0
                    };

                    if (month != 0)
                    {
                        try
                        {
                            birthDate = new DateTime(year, month, day);
                            parseSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Invalid date: {dateOfBirth}");
                        }
                    }
                }

                if (!parseSuccess)
                {
                    // Try common date formats
                    var dateFormats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "MM/dd/yyyy", "MM-dd-yyyy" };
                    foreach (var format in dateFormats)
                    {
                        if (DateTime.TryParseExact(dateOfBirth, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDate))
                        {
                            parseSuccess = true;
                            break;
                        }
                    }
                }

                if (!parseSuccess)
                {
                    _logger.LogWarning($"Could not parse Gregorian date: {dateOfBirth}");
                    return 0;
                }

                var today = DateTime.Today;
                var age = today.Year - birthDate.Year;
                
                if (birthDate.Date > today.AddYears(-age)) 
                {
                    age--;
                }

                return age;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating age from Gregorian date");
                return 0;
            }
        }

        private int CalculateAgeFromEthiopianDate(string dateOfBirth)
        {
            try
            {
                _logger.LogInformation($"Calculating age from Ethiopian date: {dateOfBirth}");

                var ethiopianPattern = @"\b(\d{1,2})[/-](\d{1,2})[/-](\d{4})\b";
                var match = Regex.Match(dateOfBirth, ethiopianPattern);
                
                if (!match.Success)
                {
                    _logger.LogWarning($"Invalid Ethiopian date format: {dateOfBirth}");
                    return 0;
                }

                var day = int.Parse(match.Groups[1].Value);
                var month = int.Parse(match.Groups[2].Value);
                var ethiopianYear = int.Parse(match.Groups[3].Value);

                // Convert Ethiopian date to approximate Gregorian date
                var gregorianYear = ethiopianYear + 8;
                if (month >= 1 && month <= 4)
                {
                    gregorianYear = ethiopianYear + 7;
                }

                DateTime birthDate;
                try
                {
                    birthDate = new DateTime(gregorianYear, month, day);
                    
                    // Adjust for Ethiopian calendar offset
                    if (month >= 1 && month <= 4)
                    {
                        birthDate = birthDate.AddMonths(8);
                    }
                    else
                    {
                        birthDate = birthDate.AddMonths(-4);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Invalid Ethiopian date: {dateOfBirth}");
                    return 0;
                }

                var today = DateTime.Today;
                var age = today.Year - birthDate.Year;
                
                if (birthDate.Date > today.AddYears(-age)) 
                {
                    age--;
                }

                return age;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating age from Ethiopian date");
                return 0;
            }
        }

        private string CleanImageData(string imageData)
        {
            if (string.IsNullOrEmpty(imageData))
                return imageData;

            if (imageData.StartsWith("data:image"))
            {
                var base64Index = imageData.IndexOf("base64,");
                if (base64Index >= 0)
                {
                    return imageData.Substring(base64Index + 7);
                }
            }
            
            return imageData;
        }
    }
}