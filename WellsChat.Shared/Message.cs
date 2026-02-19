using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WellsChat.Shared
{
    public enum MessageTypeEnum { Connected, Disconnected, Info, Me, NotMe }
    public record MessageDto
    {
        public string Payload { get; init; }
        public string SenderEmail { get; init; }
        public string SenderDisplayName { get; init; }
        public string IV { get; init; }
        public string TimeSent { get; init; }

        public static explicit operator Message(MessageDto dto)
        {
            return new Message
            {
                Payload = dto.Payload,
                SenderEmail = dto.SenderEmail,
                SenderDisplayName = dto.SenderDisplayName,
                IV = dto.IV,
                TimeSent = DateTime.Parse(dto.TimeSent).ToLocalTime(),
                TimeReceived = DateTime.UtcNow.ToLocalTime()
            };
        }
    }
    public class Message
    {
        public string Payload { get; set; }
        public string SenderEmail { get; set; }
        public string SenderDisplayName { get; set; }
        public string IV { get; set; }
        public MessageTypeEnum Type { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime TimeReceived { get; set; }
        public bool ContainsUrl
        {
            get
            {
                string url = Regex.Match(Payload, @"(https?://[^\s]+)").Value;
                return !string.IsNullOrEmpty(url);
            }
        }
        //ContainsImage
        //ContainsFile
    }
}
