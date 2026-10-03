import json
import re

log_path = "/home/lami/snap/antigravity/5/.gemini/antigravity/brain/a7a24483-ef54-43c6-aa01-4d043c95dac5/.system_generated/logs/transcript_full.jsonl"
with open(log_path, 'r') as f:
    lines = f.readlines()

out_lines = {}
found = 0
for line in lines:
    if 'TesseractOCRService.cs' in line and 'The following code has been modified' in line:
        try:
            data = json.loads(line)
            json_str = json.dumps(data)
            
            # Count how many lines we extract from this block
            block_lines = 0
            for match in re.finditer(r'\\n(\d+): (.*?)(?=\\n|$)', json_str):
                line_num = int(match.group(1))
                if line_num not in out_lines: # ONLY ADD IF WE DON'T HAVE IT (first appearance = earliest)
                    line_text = match.group(2)
                    line_text = line_text.replace('\\"', '"').replace('\\\\', '\\').replace('\\t', '\t').replace('\\r', '\r')
                    out_lines[line_num] = line_text
                    block_lines += 1
            
            if block_lines > 0:
                found += 1
        except Exception as e:
            pass

if out_lines:
    with open('/home/lami/all code/EGOVS/EGOVS/EGOVS/VotingSystem/Services/TesseractOCRService.cs', 'w') as f:
        for i in range(1, max(out_lines.keys()) + 1):
            f.write(out_lines.get(i, "") + "\n")
    print("Restored file with", len(out_lines), "lines.")
else:
    print("Could not find file contents in log.")
