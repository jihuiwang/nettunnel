using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace sharelib
{
    public class CommandFactory
    {
        public static Command Create(NetBuffer buff)
        {
            byte[] bytes = buff.GetBytes();
            string cData = Encoding.UTF8.GetString(bytes, 5, bytes.Length - 5);

            try
            {
                JObject jobj = Newtonsoft.Json.JsonConvert.DeserializeObject(cData) as JObject;
                if (jobj == null || jobj["CommandType"] == null)
                {
                    return default(Command);
                }
                Command comm = null;
                switch (jobj["CommandType"].ToString())
                {
                    case "RegisterTunnel":
                        comm = new RegisterTunnelCommand();
                        comm.Initial(jobj);
                        break;
                    case "Message":
                        comm = new MessageCommand();
                        comm.Initial(jobj);
                        break;
                    default:
                        break;
                }

                return comm;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public abstract class Command
    {
        public string CommandType { get; set; }
        public abstract Task Execute(SocketConnection conn);
        public abstract Task Initial(JObject jobj);
        public virtual byte[] GetBytes()
        {
            CommandType = string.IsNullOrWhiteSpace(CommandType) ? this.GetType().Name.Replace("Command", "") : CommandType;
            return Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(this));
        }
    }

    public class RegisterTunnelCommand : Command
    {
        public string Domain { get; set; }

        public RegisterTunnelCommand()
        {
            this.CommandType = "RegisterTunnel";
        }

        public override async Task Execute(SocketConnection conn)
        {
            CommandFrame f = new CommandFrame();            
            
            if (ConnectionManager.RegisteTunnel(Domain, conn))
            {
                f.Command = new MessageCommand() { Message = "Register " + Domain + " Success." };
            }
            else
            {
                f.Command = new MessageCommand() { Message = "Register " + Domain + " Failed." };
            }

            await f.Send(conn);
        }

        public override Task Initial(JObject jobj)
        {
            Domain = jobj["Domain"].ToString();
            return Task.CompletedTask;
        }
    }

    public class MessageCommand : Command
    {
        public string Message { get; set; }

        public MessageCommand()
        {
            this.CommandType = "Message";
        }

        public override Task Execute(SocketConnection conn)
        {
            Console.WriteLine(Message);
            return Task.CompletedTask;
        }

        public override Task Initial(JObject jobj)
        {
            Message = jobj["Message"].ToString();
            return Task.CompletedTask;
        }
    }
}
