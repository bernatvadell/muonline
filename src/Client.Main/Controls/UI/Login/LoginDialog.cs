using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Login
{
    public class LoginDialog : DialogControl
    {
        private LoginDialog()
        {
            Controls.Add(new TextureControl
            {
                TexturePath = "Interface/login_back.tga",
            });
        }
    }
}
