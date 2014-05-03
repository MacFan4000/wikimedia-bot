//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.

// Created by Petr Bena

using System;

namespace wmib
{
    public class Network : libirc.Network
    {
        private Instance instance;
        public Network (string server, Instance Instance, WmIrcProtocol protocol) : base(server, (libirc.Protocols.ProtocolIrc)protocol)
        {
            this.instance = Instance;
        }

        public override void __evt_CTCP (NetworkCTCPEventArgs args)
        {
            switch (args.CTCP)
            {
                case "FINGER":
                    Transfer("NOTICE " + args.SourceInfo.Nick + " :" + _Protocol.Separator + "FINGER" + 
                             " I am a bot don't finger me");
                    return;
                case "TIME":
                    Transfer("NOTICE " + args.SourceInfo.Nick + " :" + _Protocol.Separator + "TIME " + DateTime.Now.ToString());
                    return;
                case "PING":
                    Transfer("NOTICE " + args.SourceInfo.Nick + " :" + _Protocol.Separator + "PING" + args.Message.Substring(
                        args.Message.IndexOf(_Protocol.Separator + "PING") + 5));
                    return;
                case "VERSION":
                    Transfer("NOTICE " + args.SourceInfo.Nick + " :" + _Protocol.Separator + "VERSION " 
                             + Configuration.System.Version);
                   return;
            }
            Syslog.DebugLog("Ignoring uknown CTCP from " + args.Source + ": " + args.CTCP + args.Message);
        }

        public override bool __evt__IncomingData(IncomingDataEventArgs args)
        {
            Syslog.DebugLog(args.ServerLine);
            switch(args.Command)
            {
                case "001":
                case "002":
                    this.instance.IsWorking = true;
                    break;
            }
            return base.__evt__IncomingData(args);
        }

        public override void __evt_FinishChannelParseUser(NetworkChannelDataEventArgs args)
        {
            Channel channel = Core.GetChannel(args.ChannelName);
            Syslog.DebugLog("Finished parsing of user list for channel: " + args.ChannelName);
            if (channel != null)
                channel.HasFreshUserList = true;
        }

        public override void __evt_PRIVMSG (NetworkPRIVMSGEventArgs args)
        {
            if (args.ChannelName == null)
            {
                // private message
                // store which instance this message was from so that we can send it using same instance
                lock(Instance.TargetBuffer)
                {
                    if (!Instance.TargetBuffer.ContainsKey(args.SourceInfo.Nick))
                    {
                        Instance.TargetBuffer.Add(args.SourceInfo.Nick, this.instance);
                    } else
                    {
                        Instance.TargetBuffer[args.SourceInfo.Nick] = this.instance;
                    }
                }
                bool respond = !Commands.Trusted(args.Message, args.SourceInfo.Nick, args.SourceInfo.Host);
                string modules = "";
                lock(ExtensionHandler.Extensions)
                {
                    foreach (Module module in ExtensionHandler.Extensions)
                    {
                        if (module.IsWorking)
                        {
                            try
                            {
                                if (module.Hook_OnPrivateFromUser(args.Message, args.SourceInfo))
                                {
                                    respond = false;
                                    modules += module.Name + " ";
                                }
                            } catch (Exception fail)
                            {
                                Core.HandleException(fail);
                            }
                        }
                    }
                }
                if (respond)
                {
                    IRC.DeliverMessage("Hi, I am robot, this command was not understood." +
                                         " Please bear in mind that every message you send" +
                                         " to me will be logged for debuging purposes. See" +
                                         " documentation at http://meta.wikimedia.org/wiki" +
                                         "/WM-Bot for explanation of commands", args.SourceInfo,
                                         libirc.Defs.Priority.Low);
                    Syslog.Log("Ignoring private message: (" + args.SourceInfo.Nick + ") " + args.Message, false);
                } else
                {
                    Syslog.Log("Private message: (handled by " + modules + " from " + args.SourceInfo.Nick + ") " + 
                               args.Message, false);
                }
            } else
            {
                if (args.IsAct)
                {
                    Core.GetAction(args.Message, args.ChannelName, args.SourceInfo.Host, args.SourceInfo.Nick);
                    return;
                }
                Core.GetMessage(args.ChannelName, args.SourceInfo.Nick, args.SourceInfo.Host, args.Message);
            }
        }
    }
}

