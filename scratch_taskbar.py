import ctypes
from ctypes import wintypes

user32 = ctypes.windll.user32

class RECT(ctypes.Structure):
    _fields_ = [
        ('left', ctypes.c_long),
        ('top', ctypes.c_long),
        ('right', ctypes.c_long),
        ('bottom', ctypes.c_long)
    ]

hwnd = user32.FindWindowW("Shell_TrayWnd", None)
print("Shell_TrayWnd HWND:", hex(hwnd))

rect = RECT()
user32.GetWindowRect(hwnd, ctypes.byref(rect))
print(f"Taskbar Rect: ({rect.left}, {rect.top}, {rect.right}, {rect.bottom}) - Width: {rect.right - rect.left}, Height: {rect.bottom - rect.top}")

# Enumerate child windows
children = []
def enum_child_proc(h, l):
    buf = ctypes.create_unicode_buffer(256)
    user32.GetClassNameW(h, buf, 256)
    r = RECT()
    user32.GetWindowRect(h, ctypes.byref(r))
    if user32.IsWindowVisible(h):
        children.append((hex(h), buf.value, (r.left, r.top, r.right, r.bottom), r.right - r.left, r.bottom - r.top))
    return True

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
proc = WNDENUMPROC(enum_child_proc)
user32.EnumChildWindows(hwnd, proc, 0)

for c in children:
    print("Child:", c)
