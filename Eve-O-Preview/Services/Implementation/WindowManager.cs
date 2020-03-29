﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;
using EveOPreview.Services.Interop;

namespace EveOPreview.Services.Implementation
{
	sealed class WindowManager : IWindowManager
	{
		#region Private constants
		private const int WINDOW_SIZE_THRESHOLD = 300;
		#endregion

		public WindowManager()
		{
			// Composition is always enabled for Windows 8+
			this.IsCompositionEnabled =
				((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor >= 2)) // Win 8 and Win 8.1
				|| (Environment.OSVersion.Version.Major >= 10) // Win 10
				|| DwmNativeMethods.DwmIsCompositionEnabled(); // In case of Win 7 an API call is requiredWin 7
		}

		public bool IsCompositionEnabled { get; }

		public IntPtr GetForegroundWindowHandle()
		{
			return User32NativeMethods.GetForegroundWindow();
		}

		public void ActivateWindow(IntPtr handle)
		{
			User32NativeMethods.SetForegroundWindow(handle);

			int style = User32NativeMethods.GetWindowLong(handle, InteropConstants.GWL_STYLE);

			if ((style & InteropConstants.WS_MINIMIZE) == InteropConstants.WS_MINIMIZE)
			{
				User32NativeMethods.ShowWindowAsync(handle, InteropConstants.SW_RESTORE);
			}
		}

		public void MinimizeWindow(IntPtr handle, bool enableAnimation)
		{
			if (enableAnimation)
			{
				User32NativeMethods.SendMessage(handle, InteropConstants.WM_SYSCOMMAND, InteropConstants.SC_MINIMIZE, 0);
			}
			else
			{
				WINDOWPLACEMENT param = new WINDOWPLACEMENT();
				param.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
				User32NativeMethods.GetWindowPlacement(handle, ref param);
				param.showCmd = WINDOWPLACEMENT.SW_MINIMIZE;
				User32NativeMethods.SetWindowPlacement(handle, ref param);
			}
		}

		public void MoveWindow(IntPtr handle, int left, int top, int width, int height)
		{
			User32NativeMethods.MoveWindow(handle, left, top, width, height, true);
		}

		public void MaximizeWindow(IntPtr handle)
		{
			User32NativeMethods.ShowWindowAsync(handle, InteropConstants.SW_SHOWMAXIMIZED);
		}

		public (int Left, int Top, int Right, int Bottom) GetWindowPosition(IntPtr handle)
		{
			User32NativeMethods.GetWindowRect(handle, out RECT windowRectangle);

			return (windowRectangle.Left, windowRectangle.Top, windowRectangle.Right, windowRectangle.Bottom);
		}

		public bool IsWindowMaximized(IntPtr handle)
		{
			return User32NativeMethods.IsZoomed(handle);
		}

		public bool IsWindowMinimized(IntPtr handle)
		{
			return User32NativeMethods.IsIconic(handle);
		}

		public IDwmThumbnail GetLiveThumbnail(IntPtr destination, IntPtr source)
		{
			IDwmThumbnail thumbnail = new DwmThumbnail(this);
			thumbnail.Register(destination, source);

			return thumbnail;
		}
	}
}