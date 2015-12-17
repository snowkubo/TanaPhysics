#region Using Statements
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Input;

using Jitter;
using Jitter.Dynamics;
using Jitter.Collision;
using Jitter.LinearMath;
using Jitter.Collision.Shapes;
using Jitter.Dynamics.Constraints;
using Jitter.Dynamics.Joints;

using SingleBodyConstraints = Jitter.Dynamics.Constraints.SingleBody;

#endregion

namespace Jenga.Core
{
    public enum BodyTag { DrawMe, DontDrawMe }


    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class GameMain : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        private enum Primitives { box,sphere,cylinder,cone,capsule }

        private Primitives3D.GeometricPrimitive[] primitives =
            new Primitives3D.GeometricPrimitive[5];

        private Random random = new Random();

        private Color backgroundColor = new Color(63, 66, 73);
        private bool multithread = true;
        private int activeBodies = 0;

        private GamePadState padState;
        private KeyboardState keyState;
        private MouseState mouseState;

        public BasicEffect BasicEffect { private set; get; }
        public World World { private set; get; }

        public Camera Camera { private set; get; }
        public DebugDrawer DebugDrawer { private set; get; }
        public Display Display { private set; get; }
        public List<Scenes.Scene> PhysicScenes { private set; get;   }

        private int currentScene = 0;

        RasterizerState wireframe, cullMode,normal;

        Color[] rndColors;

        #region update - global variables
        // Hold previous input states.
        KeyboardState keyboardPreviousState = new KeyboardState();
        GamePadState gamePadPreviousState = new GamePadState();
        MouseState mousePreviousState = new MouseState();

        // Store information for drag and drop
        JVector hitPoint, hitNormal;
        Jitter.Dynamics.Constraints.SingleBody.PointOnPoint grabConstraint;
        RigidBody grabBody;
        float hitDistance = 0.0f;
        int scrollWheel = 0;
        #endregion

        public GameMain()
        {
            this.IsMouseVisible = true;

            graphics = new GraphicsDeviceManager(this);
            graphics.GraphicsProfile = GraphicsProfile.HiDef;
            graphics.PreferMultiSampling = true;
            Content.RootDirectory = "Content";

            graphics.IsFullScreen = false;
            graphics.PreferredBackBufferHeight = 600;
            graphics.PreferredBackBufferWidth = 800;

            this.IsFixedTimeStep = false;
            this.graphics.SynchronizeWithVerticalRetrace = false;


            var collision = new CollisionSystemPersistentSAP();
            this.World = new World(collision); 
            this.World.AllowDeactivation = true;

            var rr = this.random;
            rndColors = new Color[20];

            for (int i = 0; i < 20; i++)
            {
                rndColors[i] = new Color((float)rr.NextDouble(), (float)rr.NextDouble(), (float)rr.NextDouble());
            }


            wireframe = new RasterizerState();
            wireframe.FillMode = FillMode.WireFrame;

            cullMode = new RasterizerState();
            cullMode.CullMode = CullMode.None;


            normal = new RasterizerState();
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            this.Camera = new Camera(this);
            this.Camera.Position = new Vector3(20, 10, 20);
            this.Camera.Target = this.Camera.Position + Vector3.Normalize(new Vector3(5, 0, 5));
            this.Components.Add(this.Camera);

            this.DebugDrawer = new DebugDrawer(this);
            this.Components.Add(this.DebugDrawer);

            this.Display = new Display(this);
            this.Display.DrawOrder = int.MaxValue;
            this.Components.Add(this.Display);

            primitives[(int)Primitives.box] = new Primitives3D.BoxPrimitive(GraphicsDevice);
            primitives[(int)Primitives.capsule] = new Primitives3D.CapsulePrimitive(GraphicsDevice);
            primitives[(int)Primitives.cone] = new Primitives3D.ConePrimitive(GraphicsDevice);
            primitives[(int)Primitives.cylinder] = new Primitives3D.CylinderPrimitive(GraphicsDevice);
            primitives[(int)Primitives.sphere] = new Primitives3D.SpherePrimitive(GraphicsDevice);

            BasicEffect = new BasicEffect(GraphicsDevice);
            BasicEffect.EnableDefaultLighting();
            BasicEffect.PreferPerPixelLighting = true;

            // senes

            this.PhysicScenes = new List<Scenes.Scene>();

            foreach (var typeinfo in typeof(GameMain).GetTypeInfo().Assembly.DefinedTypes)
            {
                if (typeinfo.Namespace == "Jenga.Core.Scenes" && !typeinfo.IsAbstract)
                {
                    var scene = (Scenes.Scene)Activator.CreateInstance(typeinfo.AsType(), this);
                    this.PhysicScenes.Add(scene);
                }
            }

            if (0 < PhysicScenes.Count)
            {
                this.PhysicScenes[currentScene].Build();
            }

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            //TODO: use this.Content to load your game content here 
        }

        private Vector3 RayTo(int x, int y)
        {
            Vector3 nearSource = new Vector3(x, y, 0);
            Vector3 farSource = new Vector3(x, y, 1);

            Matrix world = Matrix.Identity;

            Vector3 nearPoint = graphics.GraphicsDevice.Viewport.Unproject(nearSource, Camera.Projection, Camera.View, world);
            Vector3 farPoint = graphics.GraphicsDevice.Viewport.Unproject(farSource, Camera.Projection, Camera.View, world);

            Vector3 direction = farPoint - nearPoint;
            return direction;
        }

        private bool RaycastCallback(RigidBody body, JVector normal, float fraction)
        {
            return !body.IsStatic;
        }

        private bool PressedOnce(Keys key, Buttons button)
        {
            bool keyboard = keyState.IsKeyDown(key) && !keyboardPreviousState.IsKeyDown(key);

            if (key == Keys.Add)
            {
                key = Keys.OemPlus;
            }

            keyboard |= keyState.IsKeyDown(key) && !keyboardPreviousState.IsKeyDown(key);

            if (key == Keys.Subtract)
            {
                key = Keys.OemMinus;
            }

            keyboard |= keyState.IsKeyDown(key) && !keyboardPreviousState.IsKeyDown(key);

            bool gamePad = padState.IsButtonDown(button) && !gamePadPreviousState.IsButtonDown(button);

            return keyboard || gamePad;
        }


        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            padState = GamePad.GetState(PlayerIndex.One);
            keyState = Keyboard.GetState();
            mouseState = Mouse.GetState();

            // let the user escape the demo
            if (PressedOnce(Keys.Escape, Buttons.Back))
            {
                this.Exit();
            }

            // change threading mode
            if (PressedOnce(Keys.M, Buttons.A))
            {
                multithread = !multithread;
            }

            if (PressedOnce(Keys.P,Buttons.A))
            {
                var e = World.RigidBodies.GetEnumerator();
                e.MoveNext(); e.MoveNext(); e.MoveNext();
                e.MoveNext(); e.MoveNext(); e.MoveNext();
                e.MoveNext(); e.MoveNext(); e.MoveNext();
                (e.Current as RigidBody).IsStatic = true;
                e.MoveNext();
                (e.Current as RigidBody).IsStatic = true;
            }

            #region drag and drop physical objects with the mouse
            if (mouseState.LeftButton == ButtonState.Pressed &&
                mousePreviousState.LeftButton == ButtonState.Released ||
                padState.IsButtonDown(Buttons.RightThumbstickDown) &&
                gamePadPreviousState.IsButtonUp(Buttons.RightThumbstickUp))
            {
                JVector ray = Conversion.ToJitterVector(RayTo(mouseState.X, mouseState.Y));
                JVector camp = Conversion.ToJitterVector(Camera.Position);

                ray = JVector.Normalize(ray) * 100;

                float fraction;

                bool result = World.CollisionSystem.Raycast(camp, ray, RaycastCallback, out grabBody, out hitNormal, out fraction);

                if (result)
                {
                    hitPoint = camp + fraction * ray;

                    if (grabConstraint != null)
                    {
                        World.RemoveConstraint(grabConstraint);
                    }

                    JVector lanchor = hitPoint - grabBody.Position;
                    lanchor = JVector.Transform(lanchor, JMatrix.Transpose(grabBody.Orientation));

                    grabConstraint = new SingleBodyConstraints.PointOnPoint(grabBody, lanchor);
                    grabConstraint.Softness = 0.01f;
                    grabConstraint.BiasFactor = 0.1f;

                    World.AddConstraint(grabConstraint);
                    hitDistance = (Conversion.ToXNAVector(hitPoint) - Camera.Position).Length();
                    scrollWheel = mouseState.ScrollWheelValue;
                    grabConstraint.Anchor = hitPoint;
                }
            }

            if (mouseState.LeftButton == ButtonState.Pressed || padState.IsButtonDown(Buttons.RightThumbstickDown))
            {
                hitDistance += (mouseState.ScrollWheelValue - scrollWheel) * 0.01f;
                scrollWheel = mouseState.ScrollWheelValue;

                if (grabBody != null)
                {
                    Vector3 ray = RayTo(mouseState.X, mouseState.Y); ray.Normalize();
                    grabConstraint.Anchor = Conversion.ToJitterVector(Camera.Position + ray * hitDistance);
                    grabBody.IsActive = true;
                    if (!grabBody.IsStatic)
                    {
                        grabBody.LinearVelocity *= 0.98f;
                        grabBody.AngularVelocity *= 0.98f;
                    }
                }
            }
            else
            {
                if (grabConstraint != null) 
                {
                    World.RemoveConstraint(grabConstraint);
                }
                grabBody = null;
                grabConstraint = null;
            }
            #endregion

            #region switch through physic scenes
            if (PressedOnce(Keys.Add,Buttons.X))
            {
                DestroyCurrentScene();
                currentScene++;
                currentScene = currentScene % PhysicScenes.Count;
                PhysicScenes[currentScene].Build();
            }

            if (PressedOnce(Keys.Subtract, Buttons.Y))
            {
                DestroyCurrentScene();
                currentScene += PhysicScenes.Count - 1;
                currentScene = currentScene % PhysicScenes.Count;
                PhysicScenes[currentScene].Build();
            }
            #endregion

            UpdateDisplayText(gameTime);

            float step = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (1.0f / 60.0f < step)
            {
                step = 1.0f / 60.0f;
            }
            World.Step(step, multithread);

            gamePadPreviousState = padState;
            keyboardPreviousState = keyState;
            mousePreviousState = mouseState;

            base.Update(gameTime);
        }

        private float accUpdateTime = 0.0f;

        private void UpdateDisplayText(GameTime time)
        {
            accUpdateTime += (float)time.ElapsedGameTime.TotalSeconds;
            if (accUpdateTime < 0.1f)
            {
                return;
            }

            accUpdateTime = 0.0f;

            int contactCount = 0;
            foreach (Arbiter ar in World.ArbiterMap.Arbiters)
            {
                contactCount += ar.ContactList.Count;
            }

            Display.DisplayText[1] = World.CollisionSystem.ToString();

            Display.DisplayText[0] = "Current Scene: " + PhysicScenes[currentScene].ToString();
            //
            Display.DisplayText[2] = "Arbitercount: " + World.ArbiterMap.Arbiters.Count.ToString() + ";" + " Contactcount: " + contactCount.ToString();
            Display.DisplayText[3] = "Islandcount: " + World.Islands.Count.ToString();
            Display.DisplayText[4] = "Bodycount: " + World.RigidBodies.Count + " (" + activeBodies.ToString() + ")";
            Display.DisplayText[5] = (multithread) ? "Multithreaded" : "Single Threaded";

            int entries = (int)Jitter.World.DebugType.Num;
            double total = 0;

            for (int i = 0; i < entries; i++)
            {
                var type = (World.DebugType)i;

                Display.DisplayText[8 + i] = type.ToString() + ": " + ((double)World.DebugTimes[i]).ToString("0.00");

                total += World.DebugTimes[i];
            }

            Display.DisplayText[8+entries] = "------------------------------";
            Display.DisplayText[9 + entries] = "Total Physics Time: " + total.ToString("0.00");
            Display.DisplayText[10 + entries] = "Physics Framerate: " + (1000.0d / total).ToString("0") + " fps";
        }

        private void DestroyCurrentScene()
        {
            for (int i = this.Components.Count - 1; i >= 0; i--)
            {
                IGameComponent component = this.Components[i];

                if (component is Camera)
                {
                    continue;
                }
                if (component is Display)
                {
                    continue;
                }
                if (component is DebugDrawer)
                {
                    continue;
                }

                this.Components.RemoveAt(i);
            }

            World.Clear();
        }


        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            graphics.GraphicsDevice.Clear(backgroundColor);
		
            //TODO: Add your drawing code here
            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            BasicEffect.View = Camera.View;
            BasicEffect.Projection = Camera.Projection;

            activeBodies = 0;

            // Draw all shapes
            foreach (RigidBody body in World.RigidBodies)
            {
                if (body.IsActive)
                {
                    activeBodies++;
                }
                if (body.Tag is int || body.IsParticle)
                {
                    continue;
                }
                AddBodyToDrawList(body);
            }

            BasicEffect.DiffuseColor = Color.LightGray.ToVector3();

            DrawCloth();

            PhysicScenes[currentScene].Draw();

            // Draw the debug data provided by Jitter
            DrawIslands();
            DrawJitterDebugInfo();

            foreach (Primitives3D.GeometricPrimitive prim in primitives) 
                prim.Draw(BasicEffect);

            GraphicsDevice.RasterizerState = cullMode;

            // end

            base.Draw(gameTime);

            GraphicsDevice.RasterizerState = normal;

        }

        private void AddShapeToDrawList(Shape shape, JMatrix ori, JVector pos)
        {
            Primitives3D.GeometricPrimitive primitive = null;
            Matrix scaleMatrix = Matrix.Identity;

            if (shape is BoxShape)
            {
                primitive = primitives[(int)Primitives.box];
                scaleMatrix = Matrix.CreateScale(Conversion.ToXNAVector((shape as BoxShape).Size));
            }
            else if (shape is SphereShape)
            {
                primitive = primitives[(int)Primitives.sphere];
                scaleMatrix = Matrix.CreateScale((shape as SphereShape).Radius);
            }
            else if (shape is CylinderShape)
            {
                primitive = primitives[(int)Primitives.cylinder];
                CylinderShape cs = shape as CylinderShape;
                scaleMatrix = Matrix.CreateScale(cs.Radius, cs.Height, cs.Radius);
            }
            else if (shape is CapsuleShape)
            {
                primitive = primitives[(int)Primitives.capsule];
                CapsuleShape cs = shape as CapsuleShape;
                scaleMatrix = Matrix.CreateScale(cs.Radius * 2, cs.Length, cs.Radius * 2);

            }
            else if (shape is ConeShape)
            {
                ConeShape cs = shape as ConeShape;
                scaleMatrix = Matrix.CreateScale(cs.Radius, cs.Height, cs.Radius);
                primitive = primitives[(int)Primitives.cone];
            }

            if (primitive != null)
            {
                primitive.AddWorldMatrix(
                    scaleMatrix * 
                    Conversion.ToXNAMatrix(ori) *
                    Matrix.CreateTranslation(Conversion.ToXNAVector(pos)));
            }
        }

        private void AddBodyToDrawList(RigidBody rb)
        {
            if (rb.Tag is BodyTag && ((BodyTag)rb.Tag) == BodyTag.DontDrawMe) 
                return;

            bool isCompoundShape = (rb.Shape is CompoundShape);

            if (!isCompoundShape)
            {
                AddShapeToDrawList(rb.Shape, rb.Orientation, rb.Position);
            }
            else
            {
                CompoundShape cShape = rb.Shape as CompoundShape;
                JMatrix orientation = rb.Orientation;
                JVector position = rb.Position;

                foreach (var ts in cShape.Shapes)
                {
                    JVector pos = ts.Position;
                    JMatrix ori = ts.Orientation;

                    JVector.Transform(ref pos,ref orientation,out pos);
                    JVector.Add(ref pos, ref position, out pos);

                    JMatrix.Multiply(ref ori, ref orientation, out ori);

                    AddShapeToDrawList(ts.Shape, ori, pos);
                }

            }

        }

        private void DrawJitterDebugInfo()
        {
            int cc = 0;

            foreach (Constraint constr in World.Constraints)
            {
                constr.DebugDraw(DebugDrawer);
            }

            foreach (RigidBody body in World.RigidBodies)
            {
                DebugDrawer.Color = rndColors[cc % rndColors.Length];
                body.DebugDraw(DebugDrawer);
                cc++;
            }
        }

        private void Walk(DynamicTree<SoftBody.Triangle> tree, int index)
        {
            DynamicTreeNode<SoftBody.Triangle> tn = tree.Nodes[index];
            if (tn.IsLeaf())
            {
                return;
            }
            Walk(tree,tn.Child1);
            Walk(tree,tn.Child2);

            DebugDrawer.DrawAabb(tn.AABB.Min,tn.AABB.Max, Color.Red);
        }

        private void DrawDynamicTree(SoftBody cloth)
        {
            Walk(cloth.DynamicTree,cloth.DynamicTree.Root);
        }

        private void DrawIslands()
        {
            JBBox box;

            foreach (var island in World.Islands)
            {
                box = JBBox.SmallBox;

                foreach (RigidBody body in island.Bodies)
                {
                    box = JBBox.CreateMerged(box, body.BoundingBox);
                }

                DebugDrawer.DrawAabb(box.Min, box.Max, island.IsActive() ? Color.Green : Color.Yellow);

            }
        }

        private void DrawCloth()
        {

            foreach (SoftBody body in World.SoftBodies)
            {
                if (body.Tag is BodyTag && ((BodyTag)body.Tag) == BodyTag.DontDrawMe)
                {
                    return;
                }

                for (int i = 0; i < body.Triangles.Count; i++)
                {
                    DebugDrawer.DrawTriangle(body.Triangles[i].VertexBody1.Position,
                        body.Triangles[i].VertexBody2.Position,
                        body.Triangles[i].VertexBody3.Position,
                        new Color(0, 0.95f, 0, 0.5f));
                }
                //DrawDynamicTree(body);
            }
        }
    }
}

