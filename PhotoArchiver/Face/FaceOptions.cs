﻿using System;

namespace PhotoArchiver.Face
{
    public class FaceOptions
    {
        public Uri Endpoint { get; set; }

        public string Key { get; set; }

        public string PersonGroupId { get; set; }

        public double? ConfidenceThreshold { get; set; }


        public bool IsEnabled() => Key != null;
    }
}