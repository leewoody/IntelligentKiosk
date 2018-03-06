// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Client;
using System.Text;
using Newtonsoft.Json;

namespace IntelligentKioskSample.Controls
{
    class IoTClient
    {
        public async static Task Start(Dictionary<String, String> item, string ioTHubConnectString)
        {
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(ioTHubConnectString, TransportType.Http1);
                string json = JsonConvert.SerializeObject(item, Formatting.Indented);
                await deviceClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes(json)));

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in sample: " + ex.Message);
            }
        }
    }
}