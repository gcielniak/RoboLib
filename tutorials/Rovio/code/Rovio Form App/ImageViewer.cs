using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Rovio_Form_App
{
    public partial class ImageViewer : Form
    {
        public ImageViewer()
        {
            InitializeComponent();
        }

        private void ImageViewer_Load(object sender, EventArgs e)
        {
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Size = this.ClientSize;
        }

        private void ImageViewer_Resize(object sender, EventArgs e)
        {
            pictureBox1.Size = this.ClientSize;
        }

        private delegate void UpdateImageValue(Image image);

        //update the picture box content
        public void UpdateImage(Image image)
        {
            pictureBox1.Image = image;
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(delegate { UpdateImage(image); }));
            else
                this.ClientSize = pictureBox1.Image.Size;
        }
    }
}
