using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Generic;

using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Binder;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

using Newtonsoft.Json;

namespace BC2IoTCentralDemo
{
    class Program
    {
        private static double _lastTemperature = 0.0;
        private static double _lastSetTemperature = 0.0;

        private static MqttClient _mqttClient = new MqttClient("localhost");
        
        private static IConfiguration _configuration { get; set; }

        private static List<DeviceClient> _devices = new List<DeviceClient>();
        
        private static AppConfig _ac = null;
        static void Main(string[] args)
        {
            /*
                connection.json

                {
                    "AppConfig":{
                        "connectionStrings":[
                                "HostName=...",
                                "HostName=...",
                                ...
                        ]
                    }
                }
             */
            var builder = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("connection.json");
            _configuration = builder.Build();
            _ac = _configuration.GetSection("AppConfig").Get<AppConfig>();

            foreach (string cs in _ac.ConnectionStrings)
            {
                _devices.Add(DeviceClient.CreateFromConnectionString(cs));
            }

            _mqttClient.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;

            _mqttClient.Connect("iotcentralgateway");
            
            if (_mqttClient.IsConnected)
            {
                _mqttClient.Subscribe(new string[] { "#" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });
                Console.WriteLine("MQTT connected");
            }

            while (true) {}
        }

        private static async void Client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string[] topicParts = e.Topic.Split('/');

            if (topicParts.Length != 5)
            {
                return;
            }

            string device = topicParts[1];
            string sensor = topicParts[2];
            string sensorInfo = topicParts[3];
            string measurement = topicParts[4];
            string value = System.Text.Encoding.Default.GetString(e.Message);

            string data = "";
            if (device.StartsWith("push-button") && 
                sensor.Equals("thermometer") && 
                measurement.Equals("temperature"))
            {
                int i1 = device.IndexOf(':');
                if (i1 <  0)
                {
                    return;
                }

                int deviceIndex = int.Parse(device.Substring(i1 + 1));

                DeviceClient dc = _devices[deviceIndex];


                if (sensorInfo.Equals("0:1"))
                {
                    _lastTemperature = double.Parse(value);
                } 
                
                data = $"{{\"temperature\":{_lastTemperature}}}";
                
                if (data.Length > 0)
                {
                    Message payload = new Message(System.Text.Encoding.UTF8.GetBytes(data));
                    await dc.SendEventAsync(payload);
                    
                    Console.WriteLine(device);
                    Console.WriteLine(data);            
                }
            }
        }

    }

    class AppConfig
    {
        public string[] ConnectionStrings { get; set; }
    }
}
