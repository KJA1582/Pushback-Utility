using Pushback_Utility.SimConnectInterface;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Pushback_Utility.AppUI
{
    public partial class MainWindow : Window
    {
        // ------------------------------------------------------------------------------
        // FIELDS
        // ------------------------------------------------------------------------------

        public Sim sim;

        // WndProc
        private const uint WM_USER_SIMCONNECT = 0x0402;
        private IntPtr handle;

        // ------------------------------------------------------------------------------
        // METHODS
        // ------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            sim = new Sim(this);
        }

        /// <summary>
        /// Intercept for window closing
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Handle closing logic, set e.Cancel as needed
            if (sim.connected)
                sim.changeConnection(handle, WM_USER_SIMCONNECT);
        }

        /// <summary>
        /// Setup for WndProc
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            handle = (new WindowInteropHelper(this)).Handle;
            HwndSource src = HwndSource.FromHwnd(handle);
            src.AddHook(new HwndSourceHook(WndProc));
        }

        /// <summary>
        /// Simconnect client will send a win32 message when there is 
        /// a packet to process. ReceiveMessage must be called to
        /// trigger the events. This model keeps simconnect processing on the main thread.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_USER_SIMCONNECT)
            {
                sim.handleMessage();
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Connects or disconnects to/from FSX
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sim.connected)
            {
                ((Button)sender).Content = "Connect to FS";
                sim.changeConnection(handle, WM_USER_SIMCONNECT);
            }
            else
            {
                ((Button)sender).Content = "Disconnect from FS";
                sim.changeConnection(handle, WM_USER_SIMCONNECT);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            sim.selected();
        }
    }
}
