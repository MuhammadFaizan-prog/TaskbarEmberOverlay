from PIL import ImageGrab
import os

img = ImageGrab.grab()
width, height = img.size
print("Screen size:", width, height)

taskbar_crop = img.crop((0, height - 60, width, height))
output_path = r"C:\Users\DELL\.gemini\antigravity\brain\c025a426-d7e1-4905-9869-a1bc4ef1fdcd\live_taskbar_with_free_space.png"
taskbar_crop.save(output_path)
print("Saved:", output_path)

# Also capture full screen for complete context
full_path = r"C:\Users\DELL\.gemini\antigravity\brain\c025a426-d7e1-4905-9869-a1bc4ef1fdcd\live_full_screen.png"
img.save(full_path)
print("Saved:", full_path)
