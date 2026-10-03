import json

log_path = "/home/lami/snap/antigravity/5/.gemini/antigravity/brain/a7a24483-ef54-43c6-aa01-4d043c95dac5/.system_generated/logs/transcript_full.jsonl"
with open(log_path, 'r') as f:
    lines = f.readlines()

for line in lines:
    if 'TesseractOCRService.cs' in line and 'Showing lines 1599' in line:
        print("Found line!")
