﻿using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GPTSWE
{
    [Guid("E1C8CB52-89AE-447F-A169-B257C2C8C028")]
    public class GPTSWESettings : DialogPage
    {
        private string apiKey = string.Empty;
        private string model = string.Empty;

        // The property for the API key
        [Category("API Settings")]
        [DisplayName("API Key")]
        [Description("Your API key to connect to the service.")]
        public string ApiKey
        {
            get { return apiKey; }
            set { apiKey = value; }
        }

        // The property for the Model
        [Category("Model Settings")]
        [DisplayName("Model")]
        [Description("Which model you are using here.")]
        public string Model
        {
            get { return model; }
            set { model = value; }
        }

        // Override the SaveSettingsToStorage method to persist the settings
        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
        }
    }
}
