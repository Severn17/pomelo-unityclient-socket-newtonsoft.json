using System;
using System.Text;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Pomelo.Protobuf
{
    public class MsgEncoder
    {
        private JObject protos { set; get; }//The message format(like .proto file)
        private Encoder encoder { set; get; }
        private Util util { set; get; }

        public MsgEncoder(JObject protos)
        {
            if (protos == null) protos = new JObject();

            this.protos = protos;
            this.util = new Util();
        }

        /// <summary>
        /// Encode the message from server.
        /// </summary>
        /// <param name='route'>
        /// Route.
        /// </param>
        /// <param name='msg'>
        /// Message.
        /// </param>
        public byte[] encode(string route, JObject msg)
        {
            byte[] returnByte = null;
            JToken proto;
            if (this.protos.TryGetValue(route, out proto))
            {
                if (!checkMsg(msg, (JObject)proto))
                {
                    return null;
                }
                int length = Encoder.byteLength(msg.ToString()) * 2;
                int offset = 0;
                byte[] buff = new byte[length];
                offset = encodeMsg(buff, offset, (JObject)proto, msg);
                returnByte = new byte[offset];
                for (int i = 0; i < offset; i++)
                {
                    returnByte[i] = buff[i];
                }
            }
            return returnByte;
        }

        /// <summary>
        /// Check the message.
        /// </summary>
        private bool checkMsg(JObject msg, JObject proto)
        {
            foreach (string key in msg.Properties())
            {
                JObject value = (JObject)proto[key];
                JToken proto_option;
                if (value.TryGetValue("option", out proto_option))
                {
                    switch (proto_option.ToString())
                    {
                        case "required":
                            if (!msg.ContainsKey(key))
                            {
                                return false;
                            }
                            else
                            {

                            }
                            break;
                        case "optional":
                            JToken value_type;

                            JObject messages = (JObject)proto["__messages"];

                            value_type = value["type"];

                            if (msg.ContainsKey(key))
                            {
                                JToken value_proto;

                                if (messages.TryGetValue(value_type.ToString(), out value_proto) || protos.TryGetValue("message " + value_type.ToString(), out value_proto))
                                {
                                    checkMsg((JObject)msg[key], (JObject)value_proto);
                                }
                            }
                            break;
                        case "repeated":
                            JToken msg_name;
                            JToken msg_type;
                            if (value.TryGetValue("type", out value_type) && msg.TryGetValue(key, out msg_name))
                            {
                                if (((JObject)proto["__messages"]).TryGetValue(value_type.ToString(), out msg_type) || protos.TryGetValue("message " + value_type.ToString(), out msg_type))
                                {
                                    foreach (object item in msg_name)
                                    {
                                        if (!checkMsg((JObject)item, (JObject)msg_type))
                                        {
                                            return false;
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Encode the message.
        /// </summary>
        private int encodeMsg(byte[] buffer, int offset, JObject proto, JObject msg)
        {
            foreach (string key in msg.Properties())
            {
                JToken value;
                if (proto.TryGetValue(key, out value))
                {
                    JToken value_option;
                    if (((JObject)value).TryGetValue("option", out value_option))
                    {
                        switch (value_option.ToString())
                        {
                            case "required":
                            case "optional":
                                JToken value_type, value_tag;
                                if (((JObject)value).TryGetValue("type", out value_type) && ((JObject)value).TryGetValue("tag", out value_tag))
                                {
                                    offset = this.writeBytes(buffer, offset, this.encodeTag(value_type.ToString(), Convert.ToInt32(value_tag)));
                                    offset = this.encodeProp(msg[key], value_type.ToString(), offset, buffer, proto);
                                }
                                break;
                            case "repeated":
                                JToken msg_key;
                                if (msg.TryGetValue(key, out msg_key))
                                {
                                    if (msg_key.Count() > 0)
                                    {
                                        offset = encodeArray((JArray)msg_key, (JObject)value, offset, buffer, proto);
                                    }
                                }
                                break;
                        }
                    }

                }
            }
            return offset;
        }

        /// <summary>
        /// Encode the array type.
        /// </summary>
        private int encodeArray(JArray msg, JObject value, int offset, byte[] buffer, JObject proto)
        {
            JToken value_type, value_tag;
            if (value.TryGetValue("type", out value_type) && value.TryGetValue("tag", out value_tag))
            {
                if (this.util.isSimpleType(value_type.ToString()))
                {
                    offset = this.writeBytes(buffer, offset, this.encodeTag(value_type.ToString(), Convert.ToInt32(value_tag)));
                    offset = this.writeBytes(buffer, offset, Encoder.encodeUInt32((uint)msg.Count));
                    foreach (object item in msg)
                    {
                        offset = this.encodeProp(item, value_type.ToString(), offset, buffer, null);
                    }
                }
                else
                {
                    foreach (object item in msg)
                    {
                        offset = this.writeBytes(buffer, offset, this.encodeTag(value_type.ToString(), Convert.ToInt32(value_tag)));
                        offset = this.encodeProp(item, value_type.ToString(), offset, buffer, proto);
                    }
                }
            }
            return offset;
        }

        /// <summary>
        /// Encode each item in message.
        /// </summary>
        private int encodeProp(object value, string type, int offset, byte[] buffer, JObject proto)
        {
            switch (type)
            {
                case "uInt32":
                    this.writeUInt32(buffer, ref offset, value);
                    break;
                case "int32":
                case "sInt32":
                    this.writeInt32(buffer, ref offset, value);
                    break;
                case "float":
                    this.writeFloat(buffer, ref offset, value);
                    break;
                case "double":
                    this.writeDouble(buffer, ref offset, value);
                    break;
                case "string":
                    this.writeString(buffer, ref offset, value);
                    break;
                default:
                    JToken __messages;
                    JToken __message_type;

                    if (proto.TryGetValue("__messages", out __messages))
                    {
                        if (((JObject)__messages).TryGetValue(type, out __message_type) || protos.TryGetValue("message " + type, out __message_type))
                        {
                            byte[] tembuff = new byte[Encoder.byteLength(value.ToString()) * 3];
                            int length = 0;
                            length = this.encodeMsg(tembuff, length, (JObject)__message_type, (JObject)value);
                            offset = writeBytes(buffer, offset, Encoder.encodeUInt32((uint)length));
                            for (int i = 0; i < length; i++)
                            {
                                buffer[offset] = tembuff[i];
                                offset++;
                            }
                        }
                    }
                    break;
            }
            return offset;
        }

        //Encode string.
        private void writeString(byte[] buffer, ref int offset, object value)
        {
            int le = Encoding.UTF8.GetByteCount(value.ToString());
            offset = writeBytes(buffer, offset, Encoder.encodeUInt32((uint)le));
            byte[] bytes = Encoding.UTF8.GetBytes(value.ToString());
            this.writeBytes(buffer, offset, bytes);
            offset += le;
        }

        //Encode double.
        private void writeDouble(byte[] buffer, ref int offset, object value)
        {
            WriteRawLittleEndian64(buffer, offset, (ulong)BitConverter.DoubleToInt64Bits(double.Parse(value.ToString())));
            offset += 8;
        }

        //Encode float.
        private void writeFloat(byte[] buffer, ref int offset, object value)
        {
            this.writeBytes(buffer, offset, Encoder.encodeFloat(float.Parse(value.ToString())));
            offset += 4;
        }

        ////Encode UInt32.
        private void writeUInt32(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeUInt32(value.ToString()));
        }

        //Encode Int32
        private void writeInt32(byte[] buffer, ref int offset, object value)
        {
            offset = writeBytes(buffer, offset, Encoder.encodeSInt32(value.ToString()));
        }

        //Write bytes to buffer.
        private int writeBytes(byte[] buffer, int offset, byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                buffer[offset] = bytes[i];
                offset++;
            }
            return offset;
        }

        //Encode tag.
        private byte[] encodeTag(string type, int tag)
        {
            int flag = this.util.containType(type);
            return Encoder.encodeUInt32((uint)(tag << 3 | flag));
        }


        private void WriteRawLittleEndian64(byte[] buffer, int offset, ulong value)
        {
            buffer[offset++] = ((byte)value);
            buffer[offset++] = ((byte)(value >> 8));
            buffer[offset++] = ((byte)(value >> 16));
            buffer[offset++] = ((byte)(value >> 24));
            buffer[offset++] = ((byte)(value >> 32));
            buffer[offset++] = ((byte)(value >> 40));
            buffer[offset++] = ((byte)(value >> 48));
            buffer[offset++] = ((byte)(value >> 56));
        }
    }
}