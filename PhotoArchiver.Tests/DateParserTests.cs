using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace PhotoArchiver.Tests
{
    [TestClass]
    public class DateParserTests
    {
        [TestMethod]
        public void ParseDateFromFileName_Android()
        {
            DateTime date;

            Assert.IsTrue(Archiver.TryParseDate("IMG_20190525_120904.jpg", out date));
            Assert.AreEqual(new DateTime(2019, 05, 25), date);

            Assert.IsTrue(Archiver.TryParseDate("IMG_20190526_120904 1.jpg", out date));
            Assert.AreEqual(new DateTime(2019, 05, 26), date);

            Assert.IsTrue(Archiver.TryParseDate("VID_20181227_163237.mp4", out date));
            Assert.AreEqual(new DateTime(2018, 12, 27), date);
        }

        [TestMethod]
        public void ParseDateFromFileName_WindowsPhone()
        {
            DateTime date;

            Assert.IsTrue(Archiver.TryParseDate("WP_20140711_15_25_11_0_Pro.jpg", out date));
            Assert.AreEqual(new DateTime(2014, 07, 11), date);

            Assert.IsTrue(Archiver.TryParseDate("WP_20140712_15_25_11_Raw.jpg", out date));
            Assert.AreEqual(new DateTime(2014, 07, 12), date);

            Assert.IsTrue(Archiver.TryParseDate("WP_20140713_15_25_11_Raw__highres.dng", out date));
            Assert.AreEqual(new DateTime(2014, 07, 13), date);

            Assert.IsTrue(Archiver.TryParseDate("WP_20140714_15_25_11.mp4", out date));
            Assert.AreEqual(new DateTime(2014, 07, 14), date);
        }

        [TestMethod]
        public void ParseDateFromFileName_OfficeLens()
        {
            DateTime date;

            Assert.IsTrue(Archiver.TryParseDate("2018_07_01 18_41 Office Lens.jpg", out date));
            Assert.AreEqual(new DateTime(2018, 07, 01), date);

            Assert.IsTrue(Archiver.TryParseDate("5_25_18 11_39 Office Lens.jpg", out date));
            Assert.AreEqual(new DateTime(2018, 05, 25), date);

            Assert.IsTrue(Archiver.TryParseDate("Office Lens_20140919_110252.jpg", out date));
            Assert.AreEqual(new DateTime(2014, 09, 19), date);
        }
    }
}
