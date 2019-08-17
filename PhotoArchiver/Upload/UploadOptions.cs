﻿using System;

namespace PhotoArchiver.Upload
{
    public class UploadOptions
    {
        public string? Path { get; set; }

        public bool Verify { get; set; } = true;

        public bool Delete { get; set; } = false;

        public string SearchPattern { get; set; } = "**/*";

        public int Skip { get; set; } = 0;

        public int? Take { get; set; } = null;

        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;

        public bool Deduplicate { get; set; } = false;

        public int? ParallelBlockCount { get; set; }


        public void Validate()
        {
            if (Skip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Skip));
            }

            if (Take < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Take));
            }

            if (ParallelBlockCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ParallelBlockCount));
            }
        }
    }
}
