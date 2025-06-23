using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.FlaksHelper.Entities
{
    [CustomEntity("FlaksHelper/CurvedZipMover")]
    public class CurvedZipMover : Solid
    {
        private Vector2 start, target;
        private List<Vector2> controlPoints;
        private float velocity, velocityReturn;
        private bool returnToStart;
        private string spritePath;
        private string soundEvent;

        private float percent;
        private string easing, easingReturn;
        private bool drawBlackBorder;

        public static ParticleType P_Scrape = ZipMover.P_Scrape;
        public static ParticleType P_Sparks = ZipMover.P_Sparks;

        private Color ropeColor;
        private Color ropeLightColor;

        private MTexture[,] edges = new MTexture[3, 3];
        private MTexture temp = new MTexture();
        private List<MTexture> innerCogs;
        private Sprite streetlight;
        private BloomPoint bloom;
        private SoundSource sfx = new SoundSource();
        private CurvedZipMoverPathRenderer pathRenderer;

        private class CurvedZipMoverPathRenderer : Entity
        {
            public CurvedZipMover mover;

            private MTexture cog;

            private Vector2 from, to;
            private List<Vector2> controlPoints;

            private Vector2 sparkAdd;
            private float sparkDirFromA, sparkDirFromB, sparkDirToA, sparkDirToB;

            public CurvedZipMoverPathRenderer(CurvedZipMover curvedZipMover)
            {
                base.Depth = 5000;
                mover = curvedZipMover;
                from = mover.start + new Vector2(mover.Width / 2f, mover.Height / 2f);
                to = mover.target + new Vector2(mover.Width / 2f, mover.Height / 2f);
                controlPoints = mover.controlPoints;
                sparkAdd = (from - to).SafeNormalize(5f).Perpendicular();
                float num = (from - to).Angle();
                sparkDirFromA = num + MathF.PI / 8f;
                sparkDirFromB = num - MathF.PI / 8f;
                sparkDirToA = num + MathF.PI - MathF.PI / 8f;
                sparkDirToB = num + MathF.PI + MathF.PI / 8f;
                cog = GFX.Game[$"{mover.spritePath}cog"];
            }

            public void CreateSparks()
            {
                SceneAs<Level>().ParticlesBG.Emit(P_Sparks, from + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromA);
                SceneAs<Level>().ParticlesBG.Emit(P_Sparks, from - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirFromB);
                SceneAs<Level>().ParticlesBG.Emit(P_Sparks, to + sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToA);
                SceneAs<Level>().ParticlesBG.Emit(P_Sparks, to - sparkAdd + Calc.Random.Range(-Vector2.One, Vector2.One), sparkDirToB);
            }

            public override void Render()
            {
                DrawCogs(Vector2.UnitY, Color.Black);
                DrawCogs(Vector2.Zero);

                if (mover.drawBlackBorder)
                {
                    Draw.Rect(new Rectangle((int)(mover.X + mover.Shake.X - 1f), (int)(mover.Y + mover.Shake.Y - 1f), (int)mover.Width + 2, (int)mover.Height + 2), Color.Black);
                }
            }

            private Vector2 CalcBezierAt(List<Vector2> points, float t)
            {
                List<Vector2> temp = new List<Vector2>(points);
                int count = temp.Count;
                while (count > 1)
                {
                    for (int i = 0; i < count - 1; i++)
                    {
                        temp[i] = Vector2.Lerp(temp[i], temp[i + 1], t);
                    }
                    count--;
                }
                return temp[0];
            }

            private void DrawCogs(Vector2 offset, Color? colorOverride = null)
            {
                const int segments = 60;
                float rotation = mover.percent * MathF.PI * 2f;

                List<Vector2> bezierPoints = new List<Vector2> { from };
                bezierPoints.AddRange(controlPoints.Select(cp => cp + new Vector2(mover.Width / 2.0f, mover.Height / 2.0f)));
                bezierPoints.Add(to);

                for (int i = 0; i < segments; i++)
                {
                    float t0 = i / (float)segments;
                    float t1 = (i + 1) / (float)segments;

                    Vector2 p0 = CalcBezierAt(bezierPoints, t0);
                    Vector2 p1 = CalcBezierAt(bezierPoints, t1);

                    Vector2 dir = (p1 - p0).SafeNormalize();
                    Vector2 perp = dir.Perpendicular();

                    Draw.Line(p0 + perp * 3f + offset, p1 + perp * 3f + offset, mover.ropeColor);
                    Draw.Line(p0 + perp * -4f + offset, p1 + perp * -4f + offset, mover.ropeColor);
                }

                float spacing = 4f;
                float startOffset = 4f - (mover.percent * MathF.PI * 8f % 4f);
                float distanceAccum = 0f;
                Vector2 prev = CalcBezierAt(bezierPoints, 0f);

                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    Vector2 current = CalcBezierAt(bezierPoints, t);
                    float segmentLength = (current - prev).Length();
                    distanceAccum += segmentLength;

                    if (distanceAccum >= startOffset)
                    {
                        Vector2 dir = (current - prev).SafeNormalize();
                        Vector2 perp = dir.Perpendicular();

                        Vector2 top = current + perp * 4f + offset;
                        Draw.Line(top, top + dir * 2f, mover.ropeLightColor);

                        Vector2 bottom = current + perp * -4f + offset;
                        Draw.Line(bottom, bottom - dir * 2f, mover.ropeLightColor);

                        distanceAccum -= spacing;
                    }

                    prev = current;
                }
                cog.DrawCentered(from + offset, colorOverride ?? Color.White, 1f, rotation);
                cog.DrawCentered(to + offset, colorOverride ?? Color.White, 1f, rotation);
            }

        }


        public CurvedZipMover(
            Vector2 position, 
            int width, 
            int height, 
            List<Vector2> controlPoints, 
            Vector2 target, 
            string spritePath, 
            string soundEvent, 
            Color ropeColor, 
            Color ropeLightColor, 
            float velocity, 
            float velocityReturn, 
            bool returnToStart, 
            bool drawBlackBorder,
            string easing,
            string easingReturn
        )
            : base(position, width, height, safe: false)
        {
            base.Depth = -9999;

            start = Position;
            this.controlPoints = controlPoints;
            this.target = target;
            this.spritePath = spritePath;
            this.soundEvent = soundEvent;
            this.ropeColor = ropeColor;
            this.ropeLightColor = ropeLightColor;
            this.velocity = velocity;
            this.velocityReturn = velocityReturn;
            this.returnToStart = returnToStart;
            this.drawBlackBorder = drawBlackBorder;
            this.easing = easing;
            this.easingReturn = easingReturn;

            string path = $"{spritePath}light";
            string id = $"{spritePath}block";
            string key = $"{spritePath}innercog";

            Add(new Coroutine(Sequence()));
            Add(new LightOcclude());

            innerCogs = GFX.Game.GetAtlasSubtextures(key);
            Add(streetlight = new Sprite(GFX.Game, path));
            streetlight.Add("frames", "", 1f);
            streetlight.Play("frames");
            streetlight.Active = false;
            streetlight.SetAnimationFrame(1);
            streetlight.Position = new Vector2(base.Width / 2f - streetlight.Width / 2f, 0f);
            Add(bloom = new BloomPoint(1f, 6f));
            bloom.Position = new Vector2(base.Width / 2f, 4f);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    edges[i, j] = GFX.Game[id].GetSubtexture(i * 8, j * 8, 8, 8);
                }
            }

            SurfaceSoundIndex = 7;
            sfx.Position = new Vector2(base.Width, base.Height) / 2f;
            Add(sfx);
        }

        public CurvedZipMover(EntityData data, Vector2 offset)
            : this(
                data.Position + offset,
                data.Width,
                data.Height,
                data.Nodes.SkipLast(1).Select(n => n + offset).ToList(),
                data.Nodes.Last() + offset,
                data.Attr("spritePath", "objects/zipmover/"),
                data.Attr("soundEvent", "event:/game/01_forsaken_city/zip_mover"),
                Calc.HexToColor(data.Attr("ropeColor", "663931")), 
                Calc.HexToColor(data.Attr("ropeLightColor", "9b6157")),
                data.Float("velocity", 60.0f),
                data.Float("velocityReturn", 15.0f),
                data.Bool("returnToStart", true),
                data.Bool("drawBlackBorder", false),
                data.String("easing", "SineIn"),
                data.String("easingReturn", "SineIn")
            )
        {
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            scene.Add(pathRenderer = new CurvedZipMoverPathRenderer(this));
        }

        public override void Removed(Scene scene)
        {
            scene.Remove(pathRenderer);
            pathRenderer = null;
            base.Removed(scene);
        }

        public override void Update()
        {
            base.Update();
            bloom.Y = streetlight.CurrentAnimationFrame * 3;
        }

        public override void Render()
        {
            Vector2 position = Position;
            Position += base.Shake;
            Draw.Rect(base.X + 1f, base.Y + 1f, base.Width - 2f, base.Height - 2f, Color.Black);
            int num = 1;
            float num2 = 0f;
            int count = innerCogs.Count;
            for (int i = 4; (float)i <= base.Height - 4f; i += 8)
            {
                int num3 = num;
                for (int j = 4; (float)j <= base.Width - 4f; j += 8)
                {
                    int index = (int)(mod((num2 + (float)num * percent * MathF.PI * 4f) / (MathF.PI / 2f), 1f) * (float)count);
                    MTexture mTexture = innerCogs[index];
                    Rectangle rectangle = new Rectangle(0, 0, mTexture.Width, mTexture.Height);
                    Vector2 zero = Vector2.Zero;
                    if (j <= 4)
                    {
                        zero.X = 2f;
                        rectangle.X = 2;
                        rectangle.Width -= 2;
                    }
                    else if ((float)j >= base.Width - 4f)
                    {
                        zero.X = -2f;
                        rectangle.Width -= 2;
                    }

                    if (i <= 4)
                    {
                        zero.Y = 2f;
                        rectangle.Y = 2;
                        rectangle.Height -= 2;
                    }
                    else if ((float)i >= base.Height - 4f)
                    {
                        zero.Y = -2f;
                        rectangle.Height -= 2;
                    }

                    mTexture = mTexture.GetSubtexture(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, temp);
                    mTexture.DrawCentered(Position + new Vector2(j, i) + zero, Color.White * ((num < 0) ? 0.5f : 1f));
                    num = -num;
                    num2 += MathF.PI / 3f;
                }

                if (num3 == num)
                {
                    num = -num;
                }
            }

            for (int k = 0; (float)k < base.Width / 8f; k++)
            {
                for (int l = 0; (float)l < base.Height / 8f; l++)
                {
                    int num4 = ((k != 0) ? (((float)k != base.Width / 8f - 1f) ? 1 : 2) : 0);
                    int num5 = ((l != 0) ? (((float)l != base.Height / 8f - 1f) ? 1 : 2) : 0);
                    if (num4 != 1 || num5 != 1)
                    {
                        edges[num4, num5].Draw(new Vector2(base.X + (float)(k * 8), base.Y + (float)(l * 8)));
                    }
                }
            }

            base.Render();
            Position = position;
        }

        private void ScrapeParticlesCheck(Vector2 to)
        {
            if (!base.Scene.OnInterval(0.03f))
            {
                return;
            }

            bool flag = to.Y != base.ExactPosition.Y;
            bool flag2 = to.X != base.ExactPosition.X;
            if (flag && !flag2)
            {
                int num = Math.Sign(to.Y - base.ExactPosition.Y);
                Vector2 vector = ((num != 1) ? base.TopLeft : base.BottomLeft);
                int num2 = 4;
                if (num == 1)
                {
                    num2 = Math.Min((int)base.Height - 12, 20);
                }

                int num3 = (int)base.Height;
                if (num == -1)
                {
                    num3 = Math.Max(16, (int)base.Height - 16);
                }

                if (base.Scene.CollideCheck<Solid>(vector + new Vector2(-2f, num * -2)))
                {
                    for (int i = num2; i < num3; i += 8)
                    {
                        SceneAs<Level>().ParticlesFG.Emit(P_Scrape, base.TopLeft + new Vector2(0f, (float)i + (float)num * 2f), (num == 1) ? (-MathF.PI / 4f) : (MathF.PI / 4f));
                    }
                }

                if (base.Scene.CollideCheck<Solid>(vector + new Vector2(base.Width + 2f, num * -2)))
                {
                    for (int j = num2; j < num3; j += 8)
                    {
                        SceneAs<Level>().ParticlesFG.Emit(P_Scrape, base.TopRight + new Vector2(-1f, (float)j + (float)num * 2f), (num == 1) ? (MathF.PI * -3f / 4f) : (MathF.PI * 3f / 4f));
                    }
                }
            }
            else
            {
                if (!flag2 || flag)
                {
                    return;
                }

                int num4 = Math.Sign(to.X - base.ExactPosition.X);
                Vector2 vector2 = ((num4 != 1) ? base.TopLeft : base.TopRight);
                int num5 = 4;
                if (num4 == 1)
                {
                    num5 = Math.Min((int)base.Width - 12, 20);
                }

                int num6 = (int)base.Width;
                if (num4 == -1)
                {
                    num6 = Math.Max(16, (int)base.Width - 16);
                }

                if (base.Scene.CollideCheck<Solid>(vector2 + new Vector2(num4 * -2, -2f)))
                {
                    for (int k = num5; k < num6; k += 8)
                    {
                        SceneAs<Level>().ParticlesFG.Emit(P_Scrape, base.TopLeft + new Vector2((float)k + (float)num4 * 2f, -1f), (num4 == 1) ? (MathF.PI * 3f / 4f) : (MathF.PI / 4f));
                    }
                }

                if (base.Scene.CollideCheck<Solid>(vector2 + new Vector2(num4 * -2, base.Height + 2f)))
                {
                    for (int l = num5; l < num6; l += 8)
                    {
                        SceneAs<Level>().ParticlesFG.Emit(P_Scrape, base.BottomLeft + new Vector2((float)l + (float)num4 * 2f, 0f), (num4 == 1) ? (MathF.PI * -3f / 4f) : (-MathF.PI / 4f));
                    }
                }
            }
        }

        private IEnumerator Sequence()
        {
            List<Vector2> bezierPoints = new List<Vector2>();
            bezierPoints.Add(start);
            bezierPoints.AddRange(controlPoints);
            bezierPoints.Add(target);

            while (true)
            {
                if (!HasPlayerRider())
                {
                    yield return null;
                    continue;
                }

                sfx.Play(soundEvent);
                Input.Rumble(RumbleStrength.Medium, RumbleLength.Short);
                StartShaking(0.1f);
                yield return 0.1f;
                streetlight.SetAnimationFrame(3);
                StopPlayerRunIntoAnimation = false;

                float at2 = 0f;
                float speedMultiplier = velocity / 60.0f;
                float returnSpeedMultiplier = velocityReturn / 60.0f;
                while (at2 < 1f)
                {
                    yield return null;
                    at2 = Calc.Approach(at2, 1f, 2.0f * speedMultiplier * Engine.DeltaTime);
                    percent = EasingTypeFromString(easing)(at2);

                    Vector2 bezier = CalcBezierPoint(bezierPoints, percent);

                    ScrapeParticlesCheck(bezier);
                    if (Scene.OnInterval(0.1f))
                    {
                        pathRenderer.CreateSparks();
                    }

                    MoveTo(bezier);
                }

                StartShaking(0.2f);
                Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
                SceneAs<Level>().Shake();
                StopPlayerRunIntoAnimation = true;
                yield return 0.5f;
                StopPlayerRunIntoAnimation = false;
                streetlight.SetAnimationFrame(2);

                if (returnToStart)
                {
                    at2 = 0f;
                    while (at2 < 1f)
                    {
                        yield return null;
                        at2 = Calc.Approach(at2, 1f, 2.0f * returnSpeedMultiplier * Engine.DeltaTime);
                        percent = 1f - EasingTypeFromString(easingReturn)(at2);

                        Vector2 bezier = CalcBezierPoint(bezierPoints, percent);

                        MoveTo(bezier);
                    }

                    StopPlayerRunIntoAnimation = true;
                    StartShaking(0.2f);
                    streetlight.SetAnimationFrame(1);
                    yield return 0.5f;
                }
                else
                {
                    sfx.Stop();
                    StopPlayerRunIntoAnimation = true;
                    StartShaking(0.2f);
                    streetlight.SetAnimationFrame(1);
                    yield break;
                }
            }
        }


        private float mod(float x, float m)
        {
            return (x % m + m) % m;
        }

        private Vector2 CalcBezierPoint(List<Vector2> points, float t)
        {
            List<Vector2> tempPoints = new List<Vector2>(points);
            int count = tempPoints.Count;

            while (count > 1)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    tempPoints[i] = Vector2.Lerp(tempPoints[i], tempPoints[i + 1], t);
                }
                count--;
            }

            return tempPoints[0];
        }

        private Ease.Easer EasingTypeFromString(string str)
        {
            switch (str)
            {
                case "None": return Ease.Linear;
                case "Linear": return Ease.Linear;
                case "SineIn": return Ease.SineIn;
                case "SineOut": return Ease.SineOut;
                case "SineInOut": return Ease.SineInOut;
                case "QuadIn": return Ease.QuadIn;
                case "QuadOut": return Ease.QuadOut;
                case "QuadInOut": return Ease.QuadInOut;
                case "CubeIn": return Ease.CubeIn;
                case "CubeOut": return Ease.CubeOut;
                case "CubeInOut": return Ease.CubeInOut;
                case "QuintIn": return Ease.QuintIn;
                case "QuintOut": return Ease.QuintOut;
                case "QuintInOut": return Ease.QuintInOut;
                case "ExpoIn": return Ease.ExpoIn;
                case "ExpoOut": return Ease.ExpoOut;
                case "ExpoInOut": return Ease.ExpoInOut;
                case "BackIn": return Ease.BackIn;
                case "BackOut": return Ease.BackOut;
                case "BackInOut": return Ease.BackInOut;
                case "BigBackIn": return Ease.BigBackIn;
                case "BigBackOut": return Ease.BigBackOut;
                case "BigBackInOut": return Ease.BigBackInOut;
                case "ElasticIn": return Ease.ElasticIn;
                case "ElasticOut": return Ease.ElasticOut;
                case "ElasticInOut": return Ease.ElasticInOut;
                case "BounceIn": return Ease.BounceIn;
                case "BounceOut": return Ease.BounceOut;
                case "BounceInOut": return Ease.BounceInOut;
                default: return Ease.Linear;
            }
        }
    }
}
