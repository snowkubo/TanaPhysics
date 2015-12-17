using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Jitter.Dynamics;
using Jitter.LinearMath;
using Jitter.Collision.Shapes;

namespace Jenga.Core.Scenes
{
    public abstract class Scene
    {
        public GameMain Demo { get; private set; }

        public Scene(GameMain demo)
        {
            this.Demo = demo;
        }

        public abstract void Build();

        private QuadDrawer quadDrawer = null;
        protected RigidBody ground = null;

        public void AddGround()
        {
            ground = new RigidBody(new BoxShape(new JVector(100, 10.0f, 100)));
            ground.Position = new JVector(0, -5.0f, 0);
            //ground.Tag = BodyTag.DontDrawMe;
            ground.IsStatic = true; 
            ground.Material.Restitution = 1.0f;
            ground.Material.KineticFriction = 0.0f;
            Demo.World.AddBody(ground);

            //quadDrawer = new QuadDrawer(Demo,10);


            //Demo.Components.Add(quadDrawer);
        }

        public void RemoveGround()
        {
            Demo.World.RemoveBody(ground);
            Demo.Components.Remove(quadDrawer);
            quadDrawer.Dispose();
        }

        public virtual void Draw() { }

    }
}
