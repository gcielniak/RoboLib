using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Rovio
{
    class MyRobot : Robot
    {
        public MyRobot(string address, string user, string password)
            : base(address, user, password)
        { }

        public delegate void ImageReady(Image image);

        //create an image emitting event
        public event ImageReady SourceImage;

        //repeat if you need to visualise more processing steps
        //public event ImageReady GrayscaleImage;

        public void ProcessImages()
        {
            //check if we can receive responses from the robot
            try { API.Movement.GetLibNSVersion(); } // a dummy request
            catch (Exception)
            {
                //simple way of getting feedback in the form mode
                System.Windows.Forms.MessageBox.Show("Could not connect to the robot");
                return;
            }

            //endless loop
            while (true)
            {
                //capture a single image
                Bitmap image = Camera.Image;

                //perform processing
                //Bitmap grayscale_image = AForge.Imaging.Filters.Grayscale.CommonAlgorithms.Y.Apply(image);

                //emit events
                SourceImage(image);
                //GrayscaleImage(grayscale_image);
            } 
        }
    }
}
