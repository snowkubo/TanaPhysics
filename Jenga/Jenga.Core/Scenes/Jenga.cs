using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jitter;
using Microsoft.Xna.Framework;
using Jitter.Collision.Shapes;
using Jitter.Dynamics;
using Jitter.LinearMath;
using Microsoft.Xna.Framework.Graphics;
using Jitter.Collision;

namespace Jenga.Core.Scenes
{
    class Jenga : Scene
    {

        public Jenga(GameMain demo)
            : base(demo)
        {
        }

        public override void Build()
        {
            AddGround();

            int n = 20;

            for (int i = 0; i < n; i++)
            {
                bool even = (i % 2 == 0);

                for (int e = 0; e < 3; e++)
                {
                    JVector size = (even) ? new JVector(1, 1, 3) : new JVector(3, 1, 1);
                    RigidBody body = new RigidBody(new BoxShape(size));
                    var h = 1.0f;
                    var x = 0.0f + (even ? e : h);
                    var y = i + (h / 2f);
                    var z =  (even ? 1.0f : e);
                    body.Position = new JVector(x, y, z);

                    Demo.World.AddBody(body);
                }

            }

            //BoxShape bs = new BoxShape(10, 10, 0.01f);
            //RigidBody bb = new RigidBody(bs);

            //bb.Position = new JVector(10, 5, 0);

            //Demo.World.AddBody(bb);
            //bb.IsStatic = true;

        }
        

    }
}