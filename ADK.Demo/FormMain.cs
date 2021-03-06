﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADK.Demo
{
    public partial class FormMain : Form
    {
        private Sky sky;

        public FormMain()
        {
            InitializeComponent();

            sky = new Sky();
            ISkyMap map = new SkyMap();

            map.Renderers.Add(new CelestialGridRenderer(sky, map));

            skyView.SkyMap = map;
        }

        private void skyView_MouseMove(object sender, MouseEventArgs e)
        {


            Text = 
                skyView.SkyMap.Projection.Invert(e.Location).ToString() + " / " +
                skyView.SkyMap.Projection.Invert(e.Location).ToEquatorial(sky.GeoLocation, sky.LocalSiderealTime).ToString() + " / " +
                skyView.SkyMap.ViewAngle;
        }
    }
}
