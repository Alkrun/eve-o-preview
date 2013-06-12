using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace PreviewToy
{
    public partial class Preview : Form
    {
        private IntPtr m_hThumbnail;
        public IntPtr sourceWindow;
        private DwmApi.DWM_THUMBNAIL_PROPERTIES m_ThumbnailProperties;
        private bool has_been_set_up = false;
        private PreviewToyMain spawner;

        public Preview(IntPtr sourceWindow, String title, PreviewToyMain spawner, Size size) 
        {

            this.sourceWindow = sourceWindow;
            this.spawner = spawner;
            this.Size = size;

            InitializeComponent(); 
            SetUp();

            this.Text = title;
        }

        protected override void OnResize(EventArgs e)
        {
            RefreshPreview();
            base.OnResize(e);
            if (has_been_set_up)
            {
                this.spawner.set_sync_size(this.Size);
            }
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            this.spawner.set_preview_position(this.Handle, this.Location);
        }

        protected void RefreshPreview()
        {
            if (has_been_set_up)
            {
                m_ThumbnailProperties.rcDestination = new DwmApi.RECT(0, 0, ClientRectangle.Right, ClientRectangle.Bottom);
                DwmApi.DwmUpdateThumbnailProperties(m_hThumbnail, m_ThumbnailProperties);
            }
        }

        public void SetUp()
        {
            m_hThumbnail = DwmApi.DwmRegisterThumbnail(this.Handle, sourceWindow);

            m_ThumbnailProperties = new DwmApi.DWM_THUMBNAIL_PROPERTIES();
            m_ThumbnailProperties.dwFlags = DwmApi.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_VISIBLE
                + DwmApi.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_OPACITY
                + DwmApi.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_RECTDESTINATION
                + DwmApi.DWM_THUMBNAIL_PROPERTIES.DWM_TNP_SOURCECLIENTAREAONLY;
            m_ThumbnailProperties.opacity = 255;
            m_ThumbnailProperties.fVisible = true;
            m_ThumbnailProperties.fSourceClientAreaOnly = true;
            m_ThumbnailProperties.rcDestination = new DwmApi.RECT(0, 0, ClientRectangle.Right, ClientRectangle.Bottom);
            
            DwmApi.DwmUpdateThumbnailProperties(m_hThumbnail, m_ThumbnailProperties);

            has_been_set_up = true;
        }

        private void Preview_Load(object sender, EventArgs e)
        {

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            bring_client_to_foreground();
        }

        public void bring_client_to_foreground()
        {
            DwmApi.SetForegroundWindow(sourceWindow);
            int style = DwmApi.GetWindowLong(sourceWindow, DwmApi.GWL_STYLE);
            if ((style & DwmApi.WS_MAXIMIZE) == DwmApi.WS_MAXIMIZE)
            {
                //It's maximized
            }
            else if ((style & DwmApi.WS_MINIMIZE) == DwmApi.WS_MINIMIZE)
            {
                DwmApi.ShowWindowAsync(sourceWindow, DwmApi.SW_SHOWNORMAL);
            }
        }

    }
}