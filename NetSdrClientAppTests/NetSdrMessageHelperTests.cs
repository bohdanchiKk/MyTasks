using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }


        [Test]
        public void GetControlItemMessage_WithNoneCode_ExcludesCodeBytes()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.None;
            var parameters = new byte[] { 0x01, 0x02, 0x03 };

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(5)); // 2 header + 3 parameters (no code bytes)
            Assert.That(msg[0], Is.Not.EqualTo(0)); // Header should not be zero
        }

        [Test]
        public void GetControlItemMessage_WithValidCode_IncludesCodeBytes()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var parameters = new byte[] { 0x01, 0x02, 0x03 };

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(7)); // 2 header + 2 code + 3 parameters
            var codeBytes = msg.Skip(2).Take(2).ToArray();
            var actualCode = (NetSdrMessageHelper.ControlItemCodes)BitConverter.ToUInt16(codeBytes);
            Assert.That(actualCode, Is.EqualTo(code));
        }

        [Test]
        public void TranslateMessage_ControlItemMessage_Success()
        {
            // Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var originalCode = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            var originalParameters = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var message = NetSdrMessageHelper.GetControlItemMessage(originalType, originalCode, originalParameters);

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(message, 
                out var translatedType, 
                out var translatedCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(translatedType, Is.EqualTo(originalType));
            Assert.That(translatedCode, Is.EqualTo(originalCode));
            Assert.That(sequenceNumber, Is.EqualTo(0));
            Assert.That(body, Is.EqualTo(originalParameters));
        }

        [Test]
        public void TranslateMessage_DataItemMessage_Success()
        {
            // Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.DataItem1;
            var originalParameters = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var message = NetSdrMessageHelper.GetDataItemMessage(originalType, originalParameters);

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(message, 
                out var translatedType, 
                out var translatedCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(translatedType, Is.EqualTo(originalType));
            Assert.That(translatedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(sequenceNumber, Is.Not.EqualTo(0)); // Should have sequence number
            Assert.That(body, Is.EqualTo(originalParameters));
        }

        [Test]
        public void TranslateMessage_InvalidControlItemCode_ReturnsFalse()
        {
            // Arrange - Create a message with invalid control item code
            var header = BitConverter.GetBytes((ushort)(0x2000)); // Type = 1, Length = 4
            var invalidCode = new byte[] { 0xFF, 0xFF }; // Invalid code
            var parameters = new byte[] { 0x01, 0x02 };
            var message = header.Concat(invalidCode).Concat(parameters).ToArray();

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(message, 
                out var translatedType, 
                out var translatedCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.False);
            Assert.That(translatedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
        }

        [Test]
        public void TranslateMessage_BodyLengthMismatch_ReturnsFalse()
        {
            // Arrange - Create message where body length doesn't match header
            var header = BitConverter.GetBytes((ushort)(0x2004)); // Type = 1, Length = 4
            var code = new byte[] { 0x18, 0x00 }; // ReceiverState
            var parameters = new byte[] { 0x01 }; // Only 1 byte instead of 2
            var message = header.Concat(code).Concat(parameters).ToArray();

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(message, 
                out var translatedType, 
                out var translatedCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.False);
        }

        [Test]
        public void GetSamples_16BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            ushort sampleSize = 16;
            byte[] body = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03, 0x00 }; // Three 16-bit samples

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(3));
            Assert.That(samples[0], Is.EqualTo(1));
            Assert.That(samples[1], Is.EqualTo(2));
            Assert.That(samples[2], Is.EqualTo(3));
        }

        [Test]
        public void GetSamples_8BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            ushort sampleSize = 8;
            byte[] body = new byte[] { 0x01, 0x02, 0x03 }; // Three 8-bit samples

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(3));
            Assert.That(samples[0], Is.EqualTo(1));
            Assert.That(samples[1], Is.EqualTo(2));
            Assert.That(samples[2], Is.EqualTo(3));
        }

        [Test]
        public void GetSamples_InvalidSampleSize_ThrowsException()
        {
            // Arrange
            ushort invalidSampleSize = 40; // Not 8, 16, or 32
            byte[] body = new byte[] { 0x01, 0x02 };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                NetSdrMessageHelper.GetSamples(invalidSampleSize, body).ToArray());
        }
    }
       
}