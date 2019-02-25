using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Services;
using EveOPreview.UI.Hotkeys;

namespace EveOPreview.View
{
	public partial class ThumbnailView : Form, IThumbnailView
	{
		#region Private constants
		private const int RESIZE_EVENT_TIMEOUT = 500;
		#endregion

		#region Private fields
		private readonly IWindowManager _windowManager;
		private readonly ThumbnailOverlay _overlay;
		private IDwmThumbnail _thumbnail;

		// Part of the logic (namely current size / position management)
		// was moved to the view due to the performance reasons
		private bool _isOverlayVisible;
		private bool _isTopMost;
		private bool _isLocationChanged;
		private bool _isSizeChanged;
		private bool _isCustomMouseModeActive;
		private bool _isHighlightEnabled;
		private int _highlightWidth;
		private DateTime _suppressResizeEventsTimestamp;
		private Size _baseZoomSize;
		private Point _baseZoomLocation;
		private Point _baseMousePosition;
		private Size _baseZoomMaximumSize;

		private HotkeyHandler _hotkeyHandler;
		#endregion

		public ThumbnailView(IWindowManager windowManager)
		{
			this.SuppressResizeEvent();

			this._windowManager = windowManager;

			this.IsActive = false;

			this.IsOverlayEnabled = false;
			this._isOverlayVisible = false;
			this._isTopMost = false;

			this._isLocationChanged = true;
			this._isSizeChanged = true;
			this._isCustomMouseModeActive = false;

			this._isHighlightEnabled = false;

			InitializeComponent();

			this._overlay = new ThumbnailOverlay(this, this.MouseDown_Handler);
		}

		public IntPtr Id { get; set; }

		public string Title
		{
			get => this.Text;
			set
			{
				this.Text = value;
				this._overlay.SetOverlayLabel(value);
			}
		}

		public bool IsActive { get; set; }

		public bool IsOverlayEnabled { get; set; }

		public Point ThumbnailLocation
		{
			get => this.Location;
			set
			{
				if ((value.X > 0) || (value.Y > 0))
				{
					this.StartPosition = FormStartPosition.Manual;
				}
				this.Location = value;
			}
		}

		public Size ThumbnailSize
		{
			get => this.ClientSize;
			set => this.ClientSize = value;
		}

		public Action<IntPtr> ThumbnailResized { get; set; }

		public Action<IntPtr> ThumbnailMoved { get; set; }

		public Action<IntPtr> ThumbnailFocused { get; set; }

		public Action<IntPtr> ThumbnailLostFocus { get; set; }

		public Action<IntPtr> ThumbnailActivated { get; set; }

		public Action<IntPtr> ThumbnailDeactivated { get; set; }

		public new void Show()
		{
			this.SuppressResizeEvent();

			base.Show();

			this._isLocationChanged = true;
			this._isSizeChanged = true;
			this._isOverlayVisible = false;

			this.Refresh(true);

			this.IsActive = true;
		}

		public new void Hide()
		{
			this.SuppressResizeEvent();

			this.IsActive = false;

			this._overlay.Hide();
			base.Hide();
		}

		public new void Close()
		{
			this.SuppressResizeEvent();

			this.IsActive = false;
			this._thumbnail?.Unregister();
			this._overlay.Close();
			base.Close();
		}

		// This method is used to determine if the provided Handle is related to client or its thumbnail
		public bool IsKnownHandle(IntPtr handle)
		{
			return (this.Id == handle) || (this.Handle == handle) || (this._overlay.Handle == handle);
		}

		public void SetSizeLimitations(Size minimumSize, Size maximumSize)
		{
			this.MinimumSize = minimumSize;
			this.MaximumSize = maximumSize;
		}

		public void SetOpacity(double opacity)
		{
			this.Opacity = opacity;

			// Overlay opacity settings
			// Of the thumbnail's opacity is almost full then set the overlay's one to
			// full. Otherwise set it to half of the thumnail opacity
			// Opacity value is stored even if the overlay is not displayed atm
			this._overlay.Opacity = this.Opacity > 0.9 ? 1.0 : 1.0 - (1.0 - this.Opacity) / 2;
		}

		public void SetFrames(bool enable)
		{
			FormBorderStyle style = enable ? FormBorderStyle.SizableToolWindow : FormBorderStyle.None;

			// No need to change the borders style if it is ALREADY correct
			if (this.FormBorderStyle == style)
			{
				return;
			}

			this.SuppressResizeEvent();

			this.FormBorderStyle = style;

			// Notify about possible contents position change
			this._isSizeChanged = true;
		}

		public void SetTopMost(bool enableTopmost)
		{
			if (this._isTopMost == enableTopmost)
			{
				return;
			}

			this.TopLevel = enableTopmost;
			this.TopMost = enableTopmost;
			this._overlay.TopMost = enableTopmost;

			this._isTopMost = enableTopmost;
		}

		public void SetHighlight(bool enabled, Color color, int width)
		{
			if (this._isHighlightEnabled == enabled)
			{
				return;
			}

			if (enabled)
			{
				this._isHighlightEnabled = true;
				this._highlightWidth = width;
				this.BackColor = color;
			}
			else
			{
				this._isHighlightEnabled = false;
				this.BackColor = SystemColors.Control;
			}

			this._isSizeChanged = true;
		}

		public void ZoomIn(ViewZoomAnchor anchor, int zoomFactor)
		{
			int oldWidth = this._baseZoomSize.Width;
			int oldHeight = this._baseZoomSize.Height;

			int locationX = this.Location.X;
			int locationY = this.Location.Y;

			int newWidth = (zoomFactor * this.ClientSize.Width) + (this.Size.Width - this.ClientSize.Width);
			int newHeight = (zoomFactor * this.ClientSize.Height) + (this.Size.Height - this.ClientSize.Height);

			// First change size, THEN move the window
			// Otherwise there is a chance to fail in a loop
			// Zoom requied -> Moved the windows 1st -> Focus is lost -> Window is moved back -> Focus is back on -> Zoom required -> ...
			this.MaximumSize = new Size(0, 0);
			this.Size = new Size(newWidth, newHeight);

			switch (anchor)
			{
				case ViewZoomAnchor.NW:
					break;
				case ViewZoomAnchor.N:
					this.Location = new Point(locationX - newWidth / 2 + oldWidth / 2, locationY);
					break;
				case ViewZoomAnchor.NE:
					this.Location = new Point(locationX - newWidth + oldWidth, locationY);
					break;

				case ViewZoomAnchor.W:
					this.Location = new Point(locationX, locationY - newHeight / 2 + oldHeight / 2);
					break;
				case ViewZoomAnchor.C:
					this.Location = new Point(locationX - newWidth / 2 + oldWidth / 2, locationY - newHeight / 2 + oldHeight / 2);
					break;
				case ViewZoomAnchor.E:
					this.Location = new Point(locationX - newWidth + oldWidth, locationY - newHeight / 2 + oldHeight / 2);
					break;

				case ViewZoomAnchor.SW:
					this.Location = new Point(locationX, locationY - newHeight + this._baseZoomSize.Height);
					break;
				case ViewZoomAnchor.S:
					this.Location = new Point(locationX - newWidth / 2 + oldWidth / 2, locationY - newHeight + oldHeight);
					break;
				case ViewZoomAnchor.SE:
					this.Location = new Point(locationX - newWidth + oldWidth, locationY - newHeight + oldHeight);
					break;
			}
		}

		public void ZoomOut()
		{
			this.RestoreWindowSizeAndLocation();
		}

		public void RegisterHotkey(Keys hotkey)
		{
			if (this._hotkeyHandler != null)
			{
				this.UnregisterHotkey();
			}

			if (hotkey == Keys.None)
			{
				return;
			}

			this._hotkeyHandler = new HotkeyHandler(this.Handle, hotkey);
			this._hotkeyHandler.Pressed += HotkeyPressed_Handler;
			this._hotkeyHandler.Register();
		}

		public void UnregisterHotkey()
		{
			if (this._hotkeyHandler == null)
			{
				return;
			}

			this._hotkeyHandler.Unregister();
			this._hotkeyHandler.Pressed -= HotkeyPressed_Handler;
			this._hotkeyHandler.Dispose();
			this._hotkeyHandler = null;
		}

		public void Refresh(bool forceRefresh)
		{
			// To prevent flickering the old broken thumbnail is removed AFTER the new shiny one is created
			IDwmThumbnail obsoleteThumbnail = forceRefresh ? this._thumbnail : null;

			if ((this._thumbnail == null) || forceRefresh)
			{
				this.RegisterThumbnail();
			}

			bool sizeChanged = this._isSizeChanged || forceRefresh;
			bool locationChanged = this._isLocationChanged || forceRefresh;

			if (sizeChanged)
			{
				this.RecalculateThumbnailSize();

				this.UpdateThumbnail();

				this._isSizeChanged = false;
			}

			obsoleteThumbnail?.Unregister();

			this._overlay.EnableOverlayLabel(this.IsOverlayEnabled);

			if (!this._isOverlayVisible)
			{
				// One-time action to show the Overlay before it is set up
				// Otherwise its position won't be set
				this._overlay.Show();
				this._isOverlayVisible = true;
			}
			else
			{
				if (!(sizeChanged || locationChanged))
				{
					// No need to adjust in the overlay location if it is already visible and properly set
					return;
				}
			}

			Size overlaySize = this.ClientSize;
			Point overlayLocation = this.Location;

			int borderWidth = (this.Size.Width - this.ClientSize.Width) / 2;
			overlayLocation.X += borderWidth;
			overlayLocation.Y += (this.Size.Height - this.ClientSize.Height) - borderWidth;

			this._isLocationChanged = false;
			this._overlay.Size = overlaySize;
			this._overlay.Location = overlayLocation;
			this._overlay.Refresh();
		}

		private void RecalculateThumbnailSize()
		{
			// This approach would work only for square-shaped thumbnail window
			// To get PROPER results we have to do some crazy math
			//int delta = this._isHighlightEnabled ? this._highlightWidth : 0;
			//this._thumbnail.rcDestination = new RECT(0 + delta, 0 + delta, this.ClientSize.Width - delta, this.ClientSize.Height - delta);
			if (!this._isHighlightEnabled)
			{
				//No highlighting enabled, so no odd math required
				this._thumbnail.Move(0, 0, this.ClientSize.Width, this.ClientSize.Height);
				return;
			}

			int baseWidth = this.ClientSize.Width;
			int baseHeight = this.ClientSize.Height;
			double baseAspectRatio = ((double)baseWidth) / baseHeight;

			int actualHeight = baseHeight - 2 * this._highlightWidth;
			double desiredWidth = actualHeight * baseAspectRatio;
			int actualWidth = (int)Math.Round(desiredWidth, MidpointRounding.AwayFromZero);
			int highlightWidthLeft = (baseWidth - actualWidth) / 2;
			int highlightWidthRight = baseWidth - actualWidth - highlightWidthLeft;

			this._thumbnail.Move(0 + highlightWidthLeft, 0 + this._highlightWidth, baseWidth - highlightWidthRight, baseHeight - this._highlightWidth);
		}

		private void SuppressResizeEvent()
		{
			// Workaround for WinForms issue with the Resize event being fired with inconsistent ClientSize value
			// Any Resize events fired before this timestamp will be ignored
			this._suppressResizeEventsTimestamp = DateTime.UtcNow.AddMilliseconds(ThumbnailView.RESIZE_EVENT_TIMEOUT);
		}

		#region GUI events
		protected override CreateParams CreateParams
		{
			get
			{
				var Params = base.CreateParams;
				Params.ExStyle |= (int)InteropConstants.WS_EX_TOOLWINDOW;
				return Params;
			}
		}

		private void Move_Handler(object sender, EventArgs e)
		{
			this._isLocationChanged = true;
			this.ThumbnailMoved?.Invoke(this.Id);
		}

		private void Resize_Handler(object sender, EventArgs e)
		{
			if (DateTime.UtcNow < this._suppressResizeEventsTimestamp)
			{
				return;
			}

			this._isSizeChanged = true;

			this.ThumbnailResized?.Invoke(this.Id);
		}

		private void MouseEnter_Handler(object sender, EventArgs e)
		{
			this.ExitCustomMouseMode();
			this.SaveWindowSizeAndLocation();

			this.ThumbnailFocused?.Invoke(this.Id);
		}

		private void MouseLeave_Handler(object sender, EventArgs e)
		{
			this.ThumbnailLostFocus?.Invoke(this.Id);
		}

		private void MouseDown_Handler(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				if (Control.ModifierKeys == Keys.Control)
				{
					this.ThumbnailDeactivated?.Invoke(this.Id);
				}
				else
				{
					this.ThumbnailActivated?.Invoke(this.Id);
				}
			}

			if ((e.Button == MouseButtons.Right) || (e.Button == (MouseButtons.Left | MouseButtons.Right)))
			{
				this.EnterCustomMouseMode();
			}
		}

		private void MouseMove_Handler(object sender, MouseEventArgs e)
		{
			if (this._isCustomMouseModeActive)
			{
				this.ProcessCustomMouseMode(e.Button.HasFlag(MouseButtons.Left), e.Button.HasFlag(MouseButtons.Right));
			}
		}

		private void MouseUp_Handler(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				this.ExitCustomMouseMode();
			}
		}

		private void HotkeyPressed_Handler(object sender, HandledEventArgs e)
		{
			this.ThumbnailActivated?.Invoke(this.Id);

			e.Handled = true;
		}
		#endregion

		#region Thumbnail management
		private void RegisterThumbnail()
		{
			this._thumbnail = this._windowManager.RegisterThumbnail(this.Handle, this.Id);
		}

		private void UpdateThumbnail()
		{
			this._thumbnail.Update();
		}
		#endregion

		#region Custom Mouse mode
		// This pair of methods saves/restores certain window propeties
		// Methods are used to remove the 'Zoom' effect (if any) when the
		// custom resize/move mode is activated
		// Methods are kept on this level because moving to the presenter
		// the code that responds to the mouse events like movement
		// seems like a huge overkill
		private void SaveWindowSizeAndLocation()
		{
			this._baseZoomSize = this.Size;
			this._baseZoomLocation = this.Location;
			this._baseZoomMaximumSize = this.MaximumSize;
		}

		private void RestoreWindowSizeAndLocation()
		{
			this.Size = this._baseZoomSize;
			this.MaximumSize = this._baseZoomMaximumSize;
			this.Location = this._baseZoomLocation;
		}

		private void EnterCustomMouseMode()
		{
			this.RestoreWindowSizeAndLocation();

			this._isCustomMouseModeActive = true;
			this._baseMousePosition = Control.MousePosition;
		}

		private void ProcessCustomMouseMode(bool leftButton, bool rightButton)
		{
			Point mousePosition = Control.MousePosition;
			int offsetX = mousePosition.X - this._baseMousePosition.X;
			int offsetY = mousePosition.Y - this._baseMousePosition.Y;
			this._baseMousePosition = mousePosition;

			// Left + Right buttons trigger thumbnail resize
			// Right button only trigger thumbnail movement
			if (leftButton && rightButton)
			{
				this.Size = new Size(this.Size.Width + offsetX, this.Size.Height + offsetY);
				this._baseZoomSize = this.Size;
			}
			else
			{
				this.Location = new Point(this.Location.X + offsetX, this.Location.Y + offsetY);
				this._baseZoomLocation = this.Location;
			}
		}

		private void ExitCustomMouseMode()
		{
			this._isCustomMouseModeActive = false;
		}
		#endregion
	}
}