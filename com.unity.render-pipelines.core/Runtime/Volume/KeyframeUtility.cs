using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEngine.Assertions;

// TODO: Documentation
// TODO: Unit Test
// TODO: Garbage collection fix

namespace UnityEngine.Rendering
{
    /// <summary>
    /// A helper function for interpolating AnimationCurves together. In general, curves can not be directly blended
    /// because they will have keypoints at different places. InterpAnimationCurve traverses through the keypoints.
    /// If both curves have a keypoint at the same time, they keypoints are trivially lerped together. However
    /// if one curve has a keypoint at a time that is missing in the other curve (which is the most common case),
    /// InterpAnimationCurve calculates a synthetic keypoint at that time based on value and derivative, and interpolates
    /// the resulting keys.
    ///
    /// Note that this function should only be called by internal rendering code. It creates a small pool of animation
    /// curves and reuses them to avoid creating garbage. The number of curves needed is quite small, since curves only need
    /// to be used when interpolating multiple volumes together with different curve parameters. The underlying interp
    /// function isn't allowed to fail, so in the case where we run out of memory we fall back to returning a single keyframe.
    /// </summary>
    public class KeyframeUtility
    {
        static private AnimationCurve AllocAnimationCurveFromPool()
        {
            // TODO
            return new AnimationCurve();
        }

        static bool IsAnimationCurveAvailable()
        {
            // TODO
            return true;
        }

        static private Keyframe LerpSingleKeyframe(Keyframe lhs, Keyframe rhs, float t)
        {
            var ret = new Keyframe();

            ret.time = Mathf.Lerp(lhs.time, rhs.time, t);
            ret.value = Mathf.Lerp(lhs.value, rhs.value, t);
            ret.inTangent = Mathf.Lerp(lhs.inTangent, rhs.inTangent, t);
            ret.outTangent = Mathf.Lerp(lhs.outTangent, rhs.outTangent, t);
            ret.inWeight = Mathf.Lerp(lhs.inWeight, rhs.inWeight, t);
            ret.outWeight = Mathf.Lerp(lhs.outWeight, rhs.outWeight, t);

            // it's not possible to lerp the weightedMode, so use the lhs mode.
            ret.weightedMode = lhs.weightedMode;

            // Note: ret.tangentMode is deprecated, so we will use  the value from the constructor
            return ret;
        }

        /// In an animation curve, the inTangent and outTangent don't match the edge of the curve. For example,
        /// the first key might have inTangent=3.0f but the actual incoming tangent is 0.0 because the curve is
        /// clamped outside the time domain. So this helper fetches a key, but zeroes out the inTangent of the first
        /// key and the outTangent of the last key.
        static private Keyframe GetKeyfraneAndClampEdge([DisallowNull] Keyframe[] keys, int index)
        {
            var currKey = keys[index];
            if (index == 0)
            {
                currKey.inTangent = 0.0f;
            }
            if (index == keys.Length - 1)
            {
                currKey.outTangent = 0.0f;
            }
            return currKey;
        }


        /// Fetch a key from the keys list. If index<0, then expand the first key backwards to startTime. If index>=keys.length,
        /// then extend the last key to endTime. Keys must be a valid array with at least one element.
        static private Keyframe FetchKeyFromIndexClamped([DisallowNull] Keyframe[] keys, int index, float segmentStartTime, float segmentEndTime)
        {
            float startTime = Mathf.Min(segmentStartTime, keys[0].time);
            float endTime = Mathf.Max(segmentEndTime, keys[keys.Length - 1].time);

            float startValue = keys[0].value;
            float endValue = keys[keys.Length - 1].value;

            // In practice, we are lerping animcurves for post processing curves that are always clamping at the begining and the end,
            // so we are not implementing the other wrap modes like Loop, PingPong, etc.
            Keyframe ret;
            if (index < 0)
            {
                // when you are at a time either before the curve start time the value is clamped to the start time and the input tangent is ignored.
                ret = new Keyframe(startTime, startValue, 0.0f, 0.0f);
            }
            else if (index >= keys.Length)
            {
                // if we are after the end of the curve, there slope is always zero just like before the start of a curve
                var lastKey = keys[keys.Length - 1];
                ret = new Keyframe(endTime, endValue, 0.0f, 0.0f);
            }
            else
            {
                // only remaining case is that we have a proper index
                ret = GetKeyfraneAndClampEdge(keys,index);
            }
            return ret;
        }

        /// Given a desiredTime, interpoloate between two keys to find the value and derivative. This function assumes that lhsKey.time <= desiredTime <= rhsKey.time,
        /// but will return a reasonable float value if that's not the case.
        static private void EvalCurveSegmentAndDeriv(out float dstValue, out float dstDeriv, Keyframe lhsKey, Keyframe rhsKey, float desiredTime)
        {
            // This is the same epsilon used internally
            const float epsilon = 0.0001f;

            float currTime = Mathf.Clamp(desiredTime, lhsKey.time, rhsKey.time);

            // (lhsKey.time <= rhsKey.time) should always be true. But theoretically, if garbage values get passed in, the value would
            // be clamped here to epsilon, and we would still end up with a reasonable value for dx.
            float dx = Mathf.Max(rhsKey.time - lhsKey.time, epsilon);
            float dy = rhsKey.value - lhsKey.value;
            float length = 1.0f / dx;
            float lengthSqr = length * length;

            float m1 = lhsKey.outTangent;
            float m2 = rhsKey.inTangent;
            float d1 = m1 * dx;
            float d2 = m2 * dx;

            // Note: The coeffecients are calculated to match what the editor does internally. These coeffeceients expect a
            // t in the range of [0,dx]. We could change the function to accept a range between [0,1], but then this logic would
            // be different from internal editor logic which could cause subtle bugs later.

            float c0 = (d1 + d2 - dy - dy) * lengthSqr * length;
            float c1 = (dy + dy + dy - d1 - d1 - d2) * lengthSqr;
            float c2 = m1;
            float c3 = lhsKey.value;

            float t = Mathf.Clamp(currTime - lhsKey.time, 0.0f, dx);

            dstValue = (t * (t * (t * c0 + c1) + c2)) + c3;
            dstDeriv = (t * (3.0f * t * c0 + 2.0f * c1)) + c2;
        }
        
        /// lhsIndex and rhsIndex are the indices in the keys array. The lhsIndex/rhsIndex may be -1, in which it creates a synthetic first key
        /// at startTime, or beyond the length of the array, in which case it creates a synthetic key at endTime.
        static private Keyframe EvalKeyAtTime([DisallowNull] Keyframe[] keys, int lhsIndex, int rhsIndex, float startTime, float endTime, float currTime)
        {
            var lhsKey = KeyframeUtility.FetchKeyFromIndexClamped(keys, lhsIndex, startTime, endTime);
            var rhsKey = KeyframeUtility.FetchKeyFromIndexClamped(keys, rhsIndex, startTime, endTime);

            float currValue;
            float currDeriv;
            KeyframeUtility.EvalCurveSegmentAndDeriv(out currValue, out currDeriv, lhsKey, rhsKey, currTime);

            return new Keyframe(currTime, currValue, currDeriv, currDeriv);
        }

        /// <summary>
        /// Interpolates two AnimationCurves. Since both curves likely have control points at different places
        /// in the curve, this method will create a new curve from the union of times between both curves.
        /// </summary>
        /// <param name="lhsCurve">The start value.</param>
        /// <param name="rhsCurve">The end value.</param>
        /// <param name="t">The interpolation factor in range [0,1].</param>
        static public AnimationCurve InterpAnimationCurve([DisallowNull]  AnimationCurve lhsCurve, [DisallowNull] AnimationCurve rhsCurve, float t)
        {
            if (t <= 0.0f || rhsCurve.length == 0)
            {
                return lhsCurve;
            }
            else if (t >= 1.0f || lhsCurve.length == 0)
            {
                return rhsCurve;
            }
            else
            {
                // Note: If we reached this code, we are guaranteed that both lhsCruve and rhsCurve are valid with at least 1 key

                // Check if we actually have a curve available in the pool. If not, fall back to trivially choosing the closer one.
                if (!IsAnimationCurveAvailable())
                {
                    return t <= 0.5f ? lhsCurve : rhsCurve;
                }

                // first, figure out the start and end time to include both curves
                var lhsCurveKeys = lhsCurve.keys;
                var rhsCurveKeys = rhsCurve.keys;
                
                float startTime = Mathf.Min(lhsCurveKeys[0].time, rhsCurveKeys[0].time);
                float endTime = Mathf.Max(lhsCurveKeys[lhsCurve.length - 1].time, rhsCurveKeys[rhsCurve.length - 1].time);

                // we don't know how many keys the resulting curve will have (because we will compact keys that are at the exact
                // same time), but in most cases we will need the worst case number of keys. So allocate the worst case.
                int maxNumKeys = lhsCurve.length + rhsCurve.length;
                int currNumKeys = 0;
                NativeArray<Keyframe> dstKeys = new NativeArray<Keyframe>(maxNumKeys, Allocator.Temp);

                int lhsKeyCurr = 0;
                int rhsKeyCurr = 0;

                while (lhsKeyCurr < lhsCurveKeys.Length || rhsKeyCurr < rhsCurveKeys.Length)
                {
                    // the index is considered invalid once it goes off the end of the array
                    bool lhsValid = lhsKeyCurr < lhsCurveKeys.Length;
                    bool rhsValid = rhsKeyCurr < rhsCurveKeys.Length;

                    // it's actually impossible for lhsKey/rhsKey to be uninitialized, but have to
                    // add initialize here to prevent compiler erros
                    var lhsKey = new Keyframe();
                    var rhsKey = new Keyframe();
                    if (lhsValid && rhsValid)
                    {
                        lhsKey = GetKeyfraneAndClampEdge(lhsCurveKeys,lhsKeyCurr);
                        rhsKey = GetKeyfraneAndClampEdge(rhsCurveKeys,rhsKeyCurr);

                        if (lhsKey.time == rhsKey.time)
                        {
                            lhsKeyCurr++;
                            rhsKeyCurr++;
                        }
                        else if (lhsKey.time < rhsKey.time)
                        {
                            // in this case:
                            //     rhsKey[curr-1].time <= lhsKey.time <= rhsKey[curr].time
                            // so interpolate rhsKey at the lhsKey.time.
                            rhsKey = KeyframeUtility.EvalKeyAtTime(rhsCurveKeys, rhsKeyCurr - 1, rhsKeyCurr, startTime, endTime, lhsKey.time);
                            lhsKeyCurr++;
                        }
                        else
                        {
                            // only case left is (lhsKey.time > rhsKey.time)
                            Assert.IsTrue(lhsKey.time > rhsKey.time);

                            // this is the reverse of the lhs key case
                            //     lhsKey[curr-1].time <= rhsKey.time <= lhsKey[curr].time
                            // so interpolate lhsKey at the rhsKey.time.
                            lhsKey = KeyframeUtility.EvalKeyAtTime(lhsCurveKeys, lhsKeyCurr - 1, lhsKeyCurr, startTime, endTime, rhsKey.time);
                            rhsKeyCurr++;
                        }
                    }
                    else if (lhsValid)
                    {
                        // we are still processing lhsKeys, but we are out of rhsKeys, so increment lhs and evaluate rhs
                        lhsKey = GetKeyfraneAndClampEdge(lhsCurveKeys,lhsKeyCurr);

                        // rhs will be evaluated between the last rhs key and the extrapolated rhs key at the end time
                        rhsKey = KeyframeUtility.EvalKeyAtTime(rhsCurveKeys, rhsKeyCurr - 1, rhsKeyCurr, startTime, endTime, lhsKey.time);

                        lhsKeyCurr++;
                    }
                    else
                    {
                        // either lhsValid is True, rhsValid is True, or they are both True. So to miss the first two cases,
                        // right here rhsValid must be true.
                        Assert.IsTrue(rhsValid);

                        // we still have rhsKeys to lerp, but we are out of lhsKeys, to increment rhs and evaluate lhs
                        rhsKey = GetKeyfraneAndClampEdge(rhsCurveKeys,rhsKeyCurr);

                        // lhs will be evaluated between the last lhs key and the extrapolated lhs key at the end time
                        lhsKey = KeyframeUtility.EvalKeyAtTime(lhsCurveKeys, lhsKeyCurr - 1, lhsKeyCurr, startTime, endTime, rhsKey.time);

                        rhsKeyCurr++;
                    }

                    var dstKey = KeyframeUtility.LerpSingleKeyframe(lhsKey, rhsKey, t);
                    dstKeys[currNumKeys] = dstKey;
                    currNumKeys++;
                }

                var ret = KeyframeUtility.AllocAnimationCurveFromPool();

                for (int i = 0; i < currNumKeys; i++)
                {
                    ret.AddKey(dstKeys[i]);
                }

                dstKeys.Dispose();
                return ret;
            }
        }
    }
}
