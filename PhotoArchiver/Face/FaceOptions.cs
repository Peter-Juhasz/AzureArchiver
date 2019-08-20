using System;
using System.Diagnostics.CodeAnalysis;

namespace PhotoArchiver.Face
{
    public class FaceOptions
    {
        public Uri? Endpoint { get; set; }

        public string? Key { get; set; }

        public string? PersonGroupId { get; set; }

        public double? ConfidenceThreshold { get; set; }


        public bool IsEnabled() => Key != null;


        [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
        public void Validate()
        {
            if (ConfidenceThreshold < 0 || ConfidenceThreshold > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ConfidenceThreshold));
            }
        }
    }
}
