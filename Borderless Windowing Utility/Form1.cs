using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Borderless_Windowing_Utility
{
    public partial class Form1 : Form
    {
        //these are imports that make the necesary WinAPI calls available
        #region pinvoke stuff
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
      
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
      
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);
      
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
      
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
      
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
      
        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestor_Flags gaFlags);
        
        private enum GetAncestor_Flags { GetParent = 1, GetRoot = 2, GetRootOwner = 3 }
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetTitleBarInfo(IntPtr hwnd, ref TITLEBARINFO pti);
        
        [StructLayout(LayoutKind.Sequential)]
        struct TITLEBARINFO
        {
            public const int CCHILDREN_TITLEBAR = 5;
            public uint cbSize;
            public RECT rcTitleBar;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHILDREN_TITLEBAR + 1)]
            public uint[] rgstate;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int smIndex);
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, UIntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) { return SetWindowLongPtr64(hWnd, nIndex, dwNewLong); }
            else { return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToUInt32())); }
        }
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, uint dwNewLong);
        
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, UIntPtr dwNewLong);
        
        #endregion

        //which window title is selected in the listbox
        private string SelectedTitle = null;

        //initialize
        public Form1()
        {
            InitializeComponent();
            refresh();
        }

        //user selects an item in the listbox
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedTitle = listBox1.SelectedItem.ToString();
            button1.Enabled = true;
        }

        //user clicks 'Refresh' button
        private void button2_Click(object sender, EventArgs e)
        {
            refresh();
        }

        //user clicks 'Activate' button
        private void button1_Click(object sender, EventArgs e)
        {
            executeModeChange();
            refresh();
        }

        //Remove listbox selection state, Scan for windows, refresh contents of listbox
        public void refresh()
        {
            SelectedTitle = null;
            button1.Enabled = false;
            listBox1.BeginUpdate();
            listBox1.Items.Clear();
            EnumWindows(enumWindowProc, IntPtr.Zero);
            listBox1.EndUpdate();
        }

        //attempt to change the selected item to borderless windowed "mode"
        private void executeModeChange() {
            //find the window matching the title (this could potentially be problematic if there's more than one window with the same title, but in practice it's not been an issue)
            IntPtr hWnd = FindWindow(null, SelectedTitle);
            if (hWnd == null) { MessageBox.Show("Failed to acquire window handle.\nPlease try again."); }
            //set the window to a borderless style
            const int GWL_STYLE = -16; //want to change the window style
            const UInt32 WS_POPUP = 0x80000000; //to WS_POPUP, which is a window with no border
            IntPtr sult = SetWindowLongPtr(hWnd, GWL_STYLE, (UIntPtr)WS_POPUP);
            if (sult == IntPtr.Zero)
            { 
              //in some cases SWL just outright fails, so we can notify the user and abort
              MessageBox.Show("Unable to alter window style.\nSorry.");
              return;
            }
            //otherwise we need to resize and reposition the window to take up the full screen
            const uint SWP_SHOWWINDOW = 0x40; //this flag will cause SetWindowPos to refresh the state of the window so that the changes made will become active
            int screenWidth  = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
        }

        //this is the procedure passed to the window enumerator so that it can identify the active windows, extract their titles, and add them to the listbox
        private bool enumWindowProc(IntPtr hWnd, IntPtr lParam) 
        {
            //ignore self
            if (hWnd == this.Handle) { return true; }
            
            //if the window has a titlebar title and can be alt-tabbed to then it's probably one we want to list
            int size = GetWindowTextLength(hWnd);
            if (size++ > 0 && isAltTabWindow(hWnd))
            {
                //grab the window's title
                StringBuilder sb = new StringBuilder(size);
                GetWindowText(hWnd, sb, size);
                //add it to the list
                listBox1.Items.Add(sb.ToString());
            }
            return true;
        }

        //This function checks to see if a window is alt-tabbable.
        //It sometimes returns false positives for tray icons and similar, but
        //it's never caused any real problems.
        private bool isAltTabWindow(IntPtr hWnd)
        {
            //ignore windows that aren't visible
            if (!IsWindowVisible(hWnd)) { return false; }

            //check to see if this window is its own root owner
            //derived from R.Chen's method here:
            //https://blogs.msdn.microsoft.com/oldnewthing/20071008-00/?p=24863/
            IntPtr hWndWalk = IntPtr.Zero;
            IntPtr hWndTry = GetAncestor(hWnd, GetAncestor_Flags.GetRootOwner);
            while (hWndTry != hWndWalk)
            {
                hWndWalk = hWndTry;
                hWndTry = GetLastActivePopup(hWndWalk);
                if (IsWindowVisible(hWndTry)) { break; }
            }
            if (hWndWalk != hWnd) { return false; }

            //fetch the properties of the title bar
            TITLEBARINFO ti = new TITLEBARINFO();
            ti.cbSize = (uint)Marshal.SizeOf(ti);
            GetTitleBarInfo(hWnd, ref ti);
            //if the title bar is set to invisible then we don't want this window
            const uint STATE_SYSTEM_INVISIBLE = 0x8000;
            if (ti.rgstate[0] == STATE_SYSTEM_INVISIBLE) { return false; }

            //if the window style is the one used for a floating toolbar then we don't want this window
            const int GWL_EXSTYLE = -20;
            const int WS_EX_TOOLWINDOW = 0x80;
            if (GetWindowLong(hWnd, GWL_EXSTYLE) == WS_EX_TOOLWINDOW) { return false; }

            return true;
        }

        //adjust the emelements if the form gets resized
        private void Form1_Resize(object sender, EventArgs e)
        {
            Control form = (Control)sender;
            //These magic numbers are just sizes for margins in the form.
            //It seemed needlessly pedantic to give them their own variables in this case.
            listBox1.Size    = new  Size(form.Size.Width -  40, form.Size.Height - 100);
            button1.Location = new Point(form.Size.Width - 180, form.Size.Height -  82);
            button2.Location = new Point(button2.Location.X,    form.Size.Height -  82);
        }

    }
}




