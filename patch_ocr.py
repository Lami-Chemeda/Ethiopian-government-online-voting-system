import json
import re

log_path = "/home/lami/snap/antigravity/5/.gemini/antigravity/brain/a7a24483-ef54-43c6-aa01-4d043c95dac5/.system_generated/logs/transcript_full.jsonl"
with open(log_path, 'r') as f:
    lines = f.readlines()

out_lines = {}
for line in lines:
    if 'TesseractOCRService.cs' in line and 'The following code has been modified' in line:
        try:
            data = json.loads(line)
            json_str = json.dumps(data)
            
            for match in re.finditer(r'\\n(\d+): (.*?)(?=\\n|$)', json_str):
                line_num = int(match.group(1))
                if line_num not in out_lines: 
                    line_text = match.group(2)
                    line_text = line_text.replace('\\"', '"').replace('\\\\', '\\').replace('\\t', '\t').replace('\\r', '\r')
                    out_lines[line_num] = line_text
        except Exception as e:
            pass

lines_list = [out_lines.get(i, "") for i in range(1, max(out_lines.keys()) + 1)]
original_code = "\n".join(lines_list)

# 1. Fix syntax error
original_code = original_code.replace("var lines = text.Split('\\", "var lines = text.Split('\\n', StringSplitOptions.RemoveEmptyEntries)")

# 2. Add Exact Extractors at the end of ParseFrontImageData
target1 = r'                ExtractNamesFromFront\(text, lines, data\);'
repl1 = r'                ExtractDigitalIDCardFront(text, data);\n\n                ExtractNamesFromFront(text, lines, data);'
original_code = re.sub(target1, repl1, original_code, count=1)

# 3. Add Exact Extractors at the end of ParseBackImageData
target2 = r'                ExtractPhoneNumberFromBack\(text, lines, data\);'
repl2 = r'                ExtractDigitalIDCardBack(text, data);\n\n                ExtractPhoneNumberFromBack(text, lines, data);'
original_code = re.sub(target2, repl2, original_code, count=1)

# 4. Fix EOF and append methods
target_eof = """                var base64Index = imageData.IndexOf("base64,");
                if (base64Index >= 0)"""
repl_eof = """                var base64Index = imageData.IndexOf("base64,");
                if (base64Index >= 0)
                {
                    return imageData.Substring(base64Index + 7);
                }
            }
            return imageData;
        }

        private void ExtractDigitalIDCardFront(string text, EthiopianIDData data)
        {
            try
            {
                // EXACT NAME MATCHER
                var nameMatch = Regex.Match(text, @"(?:Full\s*Name|Name)[\s\S]{1,100}?\b([A-Z][a-z]+)\s+([A-Z][a-z]+)(?:\s+([A-Z][a-z]+))?\b");
                if (nameMatch.Success && string.IsNullOrEmpty(data.FirstName))
                {
                    data.FirstName = nameMatch.Groups[1].Value;
                    if (nameMatch.Groups[3].Success && !string.IsNullOrEmpty(nameMatch.Groups[3].Value))
                    {
                        data.MiddleName = nameMatch.Groups[2].Value;
                        data.LastName = nameMatch.Groups[3].Value;
                    }
                    else
                    {
                        data.LastName = nameMatch.Groups[2].Value;
                    }
                    _logger.LogInformation($"✅ DIGITAL ID EXACT: Found Name {data.FirstName} {data.MiddleName} {data.LastName}");
                }

                // EXACT DOB MATCHER
                var dobMatch = Regex.Match(text, @"(?:Date\s*of\s*Birth|Birth)[\s\S]{1,50}?\b(\d{2}[/\-\.]\d{2}[/\-\.]\d{4})\s*\|\s*(\d{4}[/\-\.][A-Za-z]{3}[/\-\.]\d{2})\b");
                if (dobMatch.Success && string.IsNullOrEmpty(data.DateOfBirth))
                {
                    data.DateOfBirth = dobMatch.Groups[1].Value.Replace("-", "/").Replace(".", "/");
                    data.CalendarType = "Ethiopian";
                    _logger.LogInformation($"✅ DIGITAL ID EXACT: Found DOB {data.DateOfBirth}");
                }

                // EXACT SEX MATCHER
                var sexMatch = Regex.Match(text, @"(?:Sex)[\s\S]{1,50}?\|\s*(Female|Male)", RegexOptions.IgnoreCase);
                if (sexMatch.Success && string.IsNullOrEmpty(data.Sex))
                {
                    data.Sex = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(sexMatch.Groups[1].Value.ToLower());
                    _logger.LogInformation($"✅ DIGITAL ID EXACT: Found Sex {data.Sex}");
                }

                // EXACT FAN MATCHER
                var fanMatch = Regex.Match(text, @"\b(\d{16})\b");
                if (fanMatch.Success && string.IsNullOrEmpty(data.NationalId))
                {
                    data.NationalId = fanMatch.Groups[1].Value;
                    _logger.LogInformation($"✅ DIGITAL ID EXACT: Found FAN {data.NationalId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractDigitalIDCardFront");
            }
        }

        private void ExtractDigitalIDCardBack(string text, EthiopianIDData data)
        {
            try
            {
                // EXACT PHONE MATCHER
                var phoneMatch = Regex.Match(text, @"(?:Phone\s*Number)[\s\S]{1,50}?\b(09\d{8})\b");
                if (phoneMatch.Success && string.IsNullOrEmpty(data.PhoneNumber))
                {
                    data.PhoneNumber = "+251" + phoneMatch.Groups[1].Value.Substring(1);
                    _logger.LogInformation($"✅ DIGITAL ID EXACT: Found Phone {data.PhoneNumber}");
                }
                
                // EXACT REGION MATCHER
                var regionMatch = Regex.Match(text, @"(?:Address)[\s\S]{1,100}?\b(Oromia|Amhara|Tigray|Somali|Afar|Dire\s*Dawa|Harari|Gambela|Benishangul|Addis\s*Ababa|SNNPR)\b", RegexOptions.IgnoreCase);
                if (regionMatch.Success && string.IsNullOrEmpty(data.Region))
                {
                    data.Region = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(regionMatch.Groups[1].Value.ToLower());
                    _logger.LogInformation($"✅ DIGITAL ID EXACT: Found Region {data.Region}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractDigitalIDCardBack");
            }
        }
    }
}"""
original_code = original_code.replace(target_eof, repl_eof)

with open('/home/lami/all code/EGOVS/EGOVS/EGOVS/VotingSystem/Services/TesseractOCRService.cs', 'w') as f:
    f.write(original_code)

print("Patch applied perfectly!")
