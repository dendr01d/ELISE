using ELISE.ELIZA.Logic;

namespace ELIZATests
{

    [TestClass]
    public class LogicTests
    {
        [TestMethod]
        public void FilterBCD_Test()
        {
            Assert.AreEqual(Hollerith.FilterBCD(""), "");
            Assert.AreEqual(Hollerith.FilterBCD("HELLO"), "HELLO");
            Assert.AreEqual(Hollerith.FilterBCD("Hello! How are you?"), "H    . H          .");

            string allValid = "0123456789=\'+ABCDEFGHI.)-JKLMNOPQR$* /STUVWXYZ,(";

            Assert.AreEqual(Hollerith.FilterBCD(allValid), allValid);
        }

        [TestMethod]
        public void Tokenize_Test()
        {
            List<string> expected = new List<string>() { "one", "two", ",", "three", "." };

            var test = Hollerith.Tokenize("one   two, three.");

            CollectionAssert.AreEqual(expected, test);
        }

        [TestMethod]
        public void TranslateInt_Test()
        {
            Assert.AreEqual(Hollerith.TranslateInt("one"), -1);
            Assert.AreEqual(Hollerith.TranslateInt("-5"), -1);
            Assert.AreEqual(Hollerith.TranslateInt("1"), 1);
            Assert.AreEqual(Hollerith.TranslateInt("9"), 9);
            Assert.AreEqual(Hollerith.TranslateInt("555"), 555);
        }
    }
}