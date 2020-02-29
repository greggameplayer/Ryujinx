﻿using ARMeilleure.Decoders;
using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;
using System;

using static ARMeilleure.Instructions.InstEmitFlowHelper;
using static ARMeilleure.Instructions.InstEmitHelper;
using static ARMeilleure.Instructions.InstEmitSimdHelper;
using static ARMeilleure.Instructions.InstEmitSimdHelper32;
using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static partial class InstEmit32
    {
        public static void Vabs_S(ArmEmitterContext context)
        {
            OpCode32SimdS op = (OpCode32SimdS)context.CurrOp;

            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarUnaryOpSimd32(context, (m) =>
                {
                    if ((op.Size & 1) == 0)
                    {
                        Operand mask = X86GetScalar(context, -0f);
                        return context.AddIntrinsic(Intrinsic.X86Andnps, mask, m);
                    }
                    else
                    {
                        Operand mask = X86GetScalar(context, -0d);
                        return context.AddIntrinsic(Intrinsic.X86Andnpd, mask, m);
                    }
                });
            }
            else
            {
                EmitScalarUnaryOpF32(context, (op1) => EmitUnaryMathCall(context, MathF.Abs, Math.Abs, op1));
            }
        }

        public static void Vabs_V(ArmEmitterContext context)
        {
            OpCode32Simd op = (OpCode32Simd)context.CurrOp;

            if (op.F)
            {
                if (Optimizations.FastFP && Optimizations.UseSse2)
                {
                    EmitVectorUnaryOpSimd32(context, (m) =>
                    {
                        if ((op.Size & 1) == 0)
                        {
                            Operand mask = X86GetScalar(context, -0f);
                            return context.AddIntrinsic(Intrinsic.X86Andnps, mask, m);
                        }
                        else
                        {
                            Operand mask = X86GetScalar(context, -0d);
                            return context.AddIntrinsic(Intrinsic.X86Andnpd, mask, m);
                        }
                    });
                }
                else
                {
                    EmitVectorUnaryOpF32(context, (op1) => EmitUnaryMathCall(context, MathF.Abs, Math.Abs, op1));
                }
            }
            else
            {
                EmitVectorUnaryOpSx32(context, (op1) => EmitAbs(context, op1));
            }
        }

        private static Operand EmitAbs(ArmEmitterContext context, Operand value)
        {
            Operand isPositive = context.ICompareGreaterOrEqual(value, Const(value.Type, 0));

            return context.ConditionalSelect(isPositive, value, context.Negate(value));
        }

        public static void Vadd_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarBinaryOpF32(context, Intrinsic.X86Addss, Intrinsic.X86Addsd);
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => context.Add(op1, op2));
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => EmitSoftFloatCall(context, SoftFloat32.FPAdd, SoftFloat64.FPAdd, op1, op2));
            }
        }

        public static void Vadd_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorBinaryOpF32(context, Intrinsic.X86Addps, Intrinsic.X86Addpd);
            }
            else if (Optimizations.FastFP)
            {
                EmitVectorBinaryOpF32(context, (op1, op2) => context.Add(op1, op2));
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) => EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPAddFpscr, SoftFloat64.FPAddFpscr, op1, op2));
            }
        }

        public static void Vadd_I(ArmEmitterContext context)
        {
            if (Optimizations.UseSse2)
            {
                OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;
                EmitVectorBinaryOpSimd32(context, (op1, op2) => context.AddIntrinsic(X86PaddInstruction[op.Size], op1, op2));
            }
            else
            {
                EmitVectorBinaryOpZx32(context, (op1, op2) => context.Add(op1, op2));
            }
        }

        public static void Vdup(ArmEmitterContext context)
        {
            OpCode32SimdDupGP op = (OpCode32SimdDupGP)context.CurrOp;

            Operand insert = GetIntA32(context, op.Rt);

            // Zero extend into an I64, then replicate. Saves the most time over elementwise inserts.
            switch (op.Size)
            {
                case 2:
                    insert = context.Multiply(context.ZeroExtend32(OperandType.I64, insert), Const(0x0000000100000001u));
                    break;
                case 1:
                    insert = context.Multiply(context.ZeroExtend16(OperandType.I64, insert), Const(0x0001000100010001u));
                    break;
                case 0:
                    insert = context.Multiply(context.ZeroExtend8(OperandType.I64, insert), Const(0x0101010101010101u));
                    break;
                default:
                    throw new InvalidOperationException("Unknown Vdup Size.");
            }

            InsertScalar(context, op.Vd, insert);
            if (op.Q)
            {
                InsertScalar(context, op.Vd + 1, insert);
            }
        }

        public static void Vdup_1(ArmEmitterContext context)
        {
            OpCode32SimdDupElem op = (OpCode32SimdDupElem)context.CurrOp;

            Operand insert = EmitVectorExtractZx32(context, op.Vm >> 1, ((op.Vm & 1) << (3 - op.Size)) + op.Index, op.Size);

            // Zero extend into an I64, then replicate. Saves the most time over elementwise inserts.
            switch (op.Size)
            {
                case 2:
                    insert = context.Multiply(context.ZeroExtend32(OperandType.I64, insert), Const(0x0000000100000001u));
                    break;
                case 1:
                    insert = context.Multiply(context.ZeroExtend16(OperandType.I64, insert), Const(0x0001000100010001u));
                    break;
                case 0:
                    insert = context.Multiply(context.ZeroExtend8(OperandType.I64, insert), Const(0x0101010101010101u));
                    break;
                default:
                    throw new InvalidOperationException("Unknown Vdup Size.");
            }

            InsertScalar(context, op.Vd, insert);
            if (op.Q)
            {
                InsertScalar(context, op.Vd | 1, insert);
            }
        }

        private static (long, long) MaskHelperByteSequence(int start, int length, int startByte)
        {
            int end = start + length;
            int b = startByte;
            long result = 0;
            long result2 = 0;
            for (int i = 0; i < 8; i++)
            {
                result |= (long)((i >= end || i < start) ? 0x80 : b++) << (i * 8);
            }
            for (int i = 8; i < 16; i++)
            {
                result2 |= (long)((i >= end || i < start) ? 0x80 : b++) << ((i - 8) * 8);
            }
            return (result2, result);
        }

        public static void Vext(ArmEmitterContext context)
        {
            OpCode32SimdExt op = (OpCode32SimdExt)context.CurrOp;
            int elems = op.GetBytesCount();
            int byteOff = op.Immediate;

            if (Optimizations.UseSsse3)
            {
                EmitVectorBinaryOpSimd32(context, (n, m) =>
                {
                    // Writing low to high of d: start <imm> into n, overlap into m.
                    // Then rotate n down by <imm>, m up by (elems)-imm.
                    // Then OR them together for the result.

                    (long nMaskHigh, long nMaskLow) = MaskHelperByteSequence(0, elems - byteOff, byteOff);
                    (long mMaskHigh, long mMaskLow) = MaskHelperByteSequence(elems - byteOff, byteOff, 0);
                    Operand nMask, mMask;
                    if (!op.Q)
                    {
                        // Do the same operation to the bytes in the top doubleword too, as our target could be in either.
                        nMaskHigh = nMaskLow + 0x0808080808080808L;
                        mMaskHigh = mMaskLow + 0x0808080808080808L;
                    }
                    nMask = X86GetElements(context, nMaskHigh, nMaskLow);
                    mMask = X86GetElements(context, mMaskHigh, mMaskLow);
                    Operand nPart = context.AddIntrinsic(Intrinsic.X86Pshufb, n, nMask);
                    Operand mPart = context.AddIntrinsic(Intrinsic.X86Pshufb, m, mMask);

                    return context.AddIntrinsic(Intrinsic.X86Por, nPart, mPart);
                });
            }
            else
            {
                Operand res = GetVecA32(op.Qd);

                for (int index = 0; index < elems; index++)
                {
                    Operand extract;

                    if (byteOff >= elems)
                    {
                        extract = EmitVectorExtractZx32(context, op.Qm, op.Im + (byteOff - elems), op.Size);
                    }
                    else
                    {
                        extract = EmitVectorExtractZx32(context, op.Qn, op.In + byteOff, op.Size);
                    }
                    byteOff++;

                    res = EmitVectorInsert(context, res, extract, op.Id + index, op.Size);
                }

                context.Copy(GetVecA32(op.Qd), res);
            }
        }

        public static void Vmov_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarUnaryOpF32(context, 0, 0);
            }
            else
            {
                EmitScalarUnaryOpF32(context, (op1) => op1);
            }
        }

        public static void Vmovn(ArmEmitterContext context)
        {
            EmitVectorUnaryNarrowOp32(context, (op1) => op1);
        }

        public static void Vneg_S(ArmEmitterContext context)
        {
            OpCode32SimdS op = (OpCode32SimdS)context.CurrOp;

            if (Optimizations.UseSse2)
            {
                EmitScalarUnaryOpSimd32(context, (m) =>
                {
                    if ((op.Size & 1) == 0)
                    {
                        Operand mask = X86GetScalar(context, -0f);
                        return context.AddIntrinsic(Intrinsic.X86Xorps, mask, m);
                    }
                    else
                    {
                        Operand mask = X86GetScalar(context, -0d);
                        return context.AddIntrinsic(Intrinsic.X86Xorpd, mask, m);
                    }
                });
            }
            else
            {
                EmitScalarUnaryOpF32(context, (op1) => context.Negate(op1));
            }
        }

        public static void Vnmul_S(ArmEmitterContext context)
        {
            OpCode32SimdRegS op = (OpCode32SimdRegS)context.CurrOp;

            if (Optimizations.UseSse2)
            {
                EmitScalarBinaryOpSimd32(context, (n, m) =>
                {
                    if ((op.Size & 1) == 0)
                    {
                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulss, n, m);
                        Operand mask = X86GetScalar(context, -0f);
                        return context.AddIntrinsic(Intrinsic.X86Xorps, mask, res);
                    }
                    else
                    {
                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulsd, n, m);
                        Operand mask = X86GetScalar(context, -0d);
                        return context.AddIntrinsic(Intrinsic.X86Xorpd, mask, res);
                    }
                });
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => context.Negate(context.Multiply(op1, op2)));
            }
        }

        public static void Vnmla_S(ArmEmitterContext context)
        {
            OpCode32SimdRegS op = (OpCode32SimdRegS)context.CurrOp;

            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarTernaryOpSimd32(context, (d, n, m) =>
                {
                    if ((op.Size & 1) == 0)
                    {
                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulss, n, m);
                        res = context.AddIntrinsic(Intrinsic.X86Addss, d, res);
                        Operand mask = X86GetScalar(context, -0f);
                        return context.AddIntrinsic(Intrinsic.X86Xorps, mask, res);
                    }
                    else
                    {
                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulsd, n, m);
                        res = context.AddIntrinsic(Intrinsic.X86Addsd, d, res);
                        Operand mask = X86GetScalar(context, -0d);
                        return context.AddIntrinsic(Intrinsic.X86Xorpd, mask, res);
                    }
                });
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return context.Negate(context.Add(op1, context.Multiply(op2, op3)));
                });
            }
            else
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPNegMulAdd, SoftFloat64.FPNegMulAdd, op1, op2, op3);
                });
            }
        }

        public static void Vnmls_S(ArmEmitterContext context)
        {
            OpCode32SimdRegS op = (OpCode32SimdRegS)context.CurrOp;

            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarTernaryOpSimd32(context, (d, n, m) =>
                {
                    if ((op.Size & 1) == 0)
                    {
                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulss, n, m);
                        Operand mask = X86GetScalar(context, -0f);
                        d = context.AddIntrinsic(Intrinsic.X86Xorps, mask, d);
                        return context.AddIntrinsic(Intrinsic.X86Addss, d, res);

                    }
                    else
                    {
                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulsd, n, m);
                        Operand mask = X86GetScalar(context, -0d);
                        d = context.AddIntrinsic(Intrinsic.X86Xorpd, mask, res);
                        return context.AddIntrinsic(Intrinsic.X86Addsd, d, res);
                    }
                });
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return context.Add(context.Negate(op1), context.Multiply(op2, op3));
                });
            }
            else
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPNegMulSub, SoftFloat64.FPNegMulSub, op1, op2, op3);
                });
            }
        }

        public static void Vneg_V(ArmEmitterContext context)
        {
            OpCode32Simd op = (OpCode32Simd)context.CurrOp;

            if (op.F)
            {
                if (Optimizations.UseSse2)
                {
                    EmitVectorUnaryOpSimd32(context, (m) =>
                    {
                        if ((op.Size & 1) == 0)
                        {
                            Operand mask = X86GetScalar(context, -0f);
                            return context.AddIntrinsic(Intrinsic.X86Xorps, mask, m);
                        }
                        else
                        {
                            Operand mask = X86GetScalar(context, -0d);
                            return context.AddIntrinsic(Intrinsic.X86Xorpd, mask, m);
                        }
                    });
                }
                else
                {
                    EmitVectorUnaryOpF32(context, (op1) => context.Negate(op1));
                }
            }
            else
            {
                EmitVectorUnaryOpSx32(context, (op1) => context.Negate(op1));
            }
        }

        public static void Vdiv_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarBinaryOpF32(context, Intrinsic.X86Divss, Intrinsic.X86Divsd);
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => context.Divide(op1, op2));
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPDiv, SoftFloat64.FPDiv, op1, op2);
                });
            }
        }

        public static void Vmaxnm_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse41)
            {
                EmitSse41MaxMinNumOpF32(context, true, true);
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => EmitSoftFloatCall(context, SoftFloat32.FPMaxNum, SoftFloat64.FPMaxNum, op1, op2));
            }
        }

        public static void Vmaxnm_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse41)
            {
                EmitSse41MaxMinNumOpF32(context, true, false);
            }
            else
            {
                EmitVectorBinaryOpSx32(context, (op1, op2) => EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMaxNumFpscr, SoftFloat64.FPMaxNumFpscr, op1, op2));
            }
        }

        public static void Vminnm_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse41)
            {
                EmitSse41MaxMinNumOpF32(context, false, true);
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => EmitSoftFloatCall(context, SoftFloat32.FPMinNum, SoftFloat64.FPMinNum, op1, op2));
            }
        }

        public static void Vminnm_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse41)
            {
                EmitSse41MaxMinNumOpF32(context, false, false);
            }
            else
            {
                EmitVectorBinaryOpSx32(context, (op1, op2) => EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMinNumFpscr, SoftFloat64.FPMinNumFpscr, op1, op2));
            }
        }

        public static void Vmax_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorBinaryOpF32(context, Intrinsic.X86Maxps, Intrinsic.X86Maxpd);
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMaxFpscr, SoftFloat64.FPMaxFpscr, op1, op2);
                });
            }
        }

        public static void Vmax_I(ArmEmitterContext context)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            if (op.U)
            {
                if (Optimizations.UseSse2)
                {
                    EmitVectorBinaryOpSimd32(context, (op1, op2) => context.AddIntrinsic(X86PmaxuInstruction[op.Size], op1, op2));
                }
                else
                {
                    EmitVectorBinaryOpZx32(context, (op1, op2) => context.ConditionalSelect(context.ICompareGreaterUI(op1, op2), op1, op2));
                }
            }
            else
            {
                if (Optimizations.UseSse2)
                {
                    EmitVectorBinaryOpSimd32(context, (op1, op2) => context.AddIntrinsic(X86PmaxsInstruction[op.Size], op1, op2));
                }
                else
                {
                    EmitVectorBinaryOpSx32(context, (op1, op2) => context.ConditionalSelect(context.ICompareGreater(op1, op2), op1, op2));
                }
            }
        }

        public static void Vmin_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorBinaryOpF32(context, Intrinsic.X86Minps, Intrinsic.X86Minpd);
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMinFpscr, SoftFloat64.FPMinFpscr, op1, op2);
                });
            }
        }

        public static void Vmin_I(ArmEmitterContext context)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            if (op.U)
            {
                if (Optimizations.UseSse2)
                {
                    EmitVectorBinaryOpSimd32(context, (op1, op2) => context.AddIntrinsic(X86PminuInstruction[op.Size], op1, op2));
                }
                else
                {
                    EmitVectorBinaryOpZx32(context, (op1, op2) => context.ConditionalSelect(context.ICompareLessUI(op1, op2), op1, op2));
                }
            }
            else
            {
                if (Optimizations.UseSse2)
                {
                    EmitVectorBinaryOpSimd32(context, (op1, op2) => context.AddIntrinsic(X86PminsInstruction[op.Size], op1, op2));
                }
                else
                {
                    EmitVectorBinaryOpSx32(context, (op1, op2) => context.ConditionalSelect(context.ICompareLess(op1, op2), op1, op2));
                }
            }
        }

        public static void Vmul_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarBinaryOpF32(context, Intrinsic.X86Mulss, Intrinsic.X86Mulsd);
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => context.Multiply(op1, op2));
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPMul, SoftFloat64.FPMul, op1, op2);
                });
            }
        }

        public static void Vmul_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorBinaryOpF32(context, Intrinsic.X86Mulps, Intrinsic.X86Mulpd);
            }
            else if (Optimizations.FastFP)
            {
                EmitVectorBinaryOpF32(context, (op1, op2) => context.Multiply(op1, op2));
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMulFpscr, SoftFloat64.FPMulFpscr, op1, op2);
                });
            }
        }

        public static void Vmul_I(ArmEmitterContext context)
        {
            if ((context.CurrOp as OpCode32SimdReg).U) throw new NotImplementedException("Polynomial mode not implemented");
            EmitVectorBinaryOpSx32(context, (op1, op2) => context.Multiply(op1, op2));
        }

        public static void Vmul_1(ArmEmitterContext context)
        {
            OpCode32SimdRegElem op = (OpCode32SimdRegElem)context.CurrOp;

            if (op.F)
            {
                if (Optimizations.FastFP && Optimizations.UseSse2)
                {
                    EmitVectorByScalarOpF32(context, Intrinsic.X86Mulps, Intrinsic.X86Mulpd);
                }
                else if (Optimizations.FastFP)
                {
                    EmitVectorByScalarOpF32(context, (op1, op2) => context.Multiply(op1, op2));
                }
                else
                {
                    EmitVectorByScalarOpF32(context, (op1, op2) => EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMulFpscr, SoftFloat64.FPMulFpscr, op1, op2));
                }
            }
            else
            {
                EmitVectorByScalarOpI32(context, (op1, op2) => context.Multiply(op1, op2), false);
            }
        }

        public static void Vmla_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarTernaryOpF32(context, Intrinsic.X86Mulss, Intrinsic.X86Mulsd, Intrinsic.X86Addss, Intrinsic.X86Addsd);
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return context.Add(op1, context.Multiply(op2, op3));
                });
            }
            else
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPMulAdd, SoftFloat64.FPMulAdd, op1, op2, op3);
                });
            }
        }

        public static void Vmla_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorTernaryOpF32(context, Intrinsic.X86Mulps, Intrinsic.X86Mulpd, Intrinsic.X86Addps, Intrinsic.X86Addpd);
            }
            else if (Optimizations.FastFP)
            {
                EmitVectorTernaryOpF32(context, (op1, op2, op3) => context.Add(op1, context.Multiply(op2, op3)));
            }
            else
            {
                EmitVectorTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMulAddFpscr, SoftFloat64.FPMulAddFpscr, op1, op2, op3);
                });
            }
        }

        public static void Vmla_I(ArmEmitterContext context)
        {
            EmitVectorTernaryOpZx32(context, (op1, op2, op3) => context.Add(op1, context.Multiply(op2, op3)));
        }

        public static void Vmla_1(ArmEmitterContext context)
        {
            OpCode32SimdRegElem op = (OpCode32SimdRegElem)context.CurrOp;

            if (op.F)
            {
                if (Optimizations.FastFP && Optimizations.UseSse2)
                {
                    EmitVectorsByScalarOpF32(context, Intrinsic.X86Mulps, Intrinsic.X86Mulpd, Intrinsic.X86Addps, Intrinsic.X86Addpd);
                }
                else if (Optimizations.FastFP)
                {
                    EmitVectorsByScalarOpF32(context, (op1, op2, op3) => context.Add(op1, context.Multiply(op2, op3)));
                }
                else
                {
                    EmitVectorsByScalarOpF32(context, (op1, op2, op3) => EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMulAddFpscr, SoftFloat64.FPMulAddFpscr, op1, op2, op3));
                }
            }
            else
            {
                EmitVectorsByScalarOpI32(context, (op1, op2, op3) => context.Add(op1, context.Multiply(op2, op3)), false);
            }
        }

        public static void Vmls_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarTernaryOpF32(context, Intrinsic.X86Mulss, Intrinsic.X86Mulsd, Intrinsic.X86Subss, Intrinsic.X86Subsd);
            }
            else if (Optimizations.FastFP)
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return context.Subtract(op1, context.Multiply(op2, op3));
                });
            }
            else
            {
                EmitScalarTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPMulSub, SoftFloat64.FPMulSub, op1, op2, op3);
                });
            }
        }

        public static void Vmls_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorTernaryOpF32(context, Intrinsic.X86Mulps, Intrinsic.X86Mulpd, Intrinsic.X86Subps, Intrinsic.X86Subpd);
            }
            else if (Optimizations.FastFP)
            {
                EmitVectorTernaryOpF32(context, (op1, op2, op3) => context.Subtract(op1, context.Multiply(op2, op3)));
            }
            else
            {
                EmitVectorTernaryOpF32(context, (op1, op2, op3) =>
                {
                    return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMulSubFpscr, SoftFloat64.FPMulSubFpscr, op1, op2, op3);
                });
            }
        }

        public static void Vmls_I(ArmEmitterContext context)
        {
            EmitVectorTernaryOpZx32(context, (op1, op2, op3) => context.Subtract(op1, context.Multiply(op2, op3)));
        }

        public static void Vmls_1(ArmEmitterContext context)
        {
            OpCode32SimdRegElem op = (OpCode32SimdRegElem)context.CurrOp;

            if (op.F)
            {
                if (Optimizations.FastFP && Optimizations.UseSse2)
                {
                    EmitVectorsByScalarOpF32(context, Intrinsic.X86Mulps, Intrinsic.X86Mulpd, Intrinsic.X86Subps, Intrinsic.X86Subpd);
                }
                else if (Optimizations.FastFP)
                {
                    EmitVectorsByScalarOpF32(context, (op1, op2, op3) => context.Subtract(op1, context.Multiply(op2, op3)));
                }
                else
                {
                    EmitVectorsByScalarOpF32(context, (op1, op2, op3) => EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPMulSubFpscr, SoftFloat64.FPMulSubFpscr, op1, op2, op3));
                }
            }
            else
            {
                EmitVectorsByScalarOpI32(context, (op1, op2, op3) => context.Subtract(op1, context.Multiply(op2, op3)), false);
            }
        }

        public static void Vpadd_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitSse2VectorPairwiseOpF32(context, Intrinsic.X86Addps);
            }
            else
            {
                EmitVectorPairwiseOpF32(context, (op1, op2) => context.Add(op1, op2));
            }
        }

        public static void Vpadd_I(ArmEmitterContext context)
        {
            OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;

            if (Optimizations.UseSsse3)
            {
                EmitSsse3VectorPairwiseOp32(context, X86PaddInstruction);
            }
            else
            {
                EmitVectorPairwiseOpI32(context, (op1, op2) => context.Add(op1, op2), !op.U);
            }
        }

        public static void Vrev(ArmEmitterContext context)
        {
            OpCode32SimdRev op = (OpCode32SimdRev)context.CurrOp;

            if (Optimizations.UseSsse3)
            {
                EmitVectorUnaryOpSimd32(context, (op1) =>
                {
                    Operand mask;
                    switch (op.Size)
                    {
                        case 3:
                            // Rev64
                            switch (op.Opc)
                            {
                                case 0:
                                    mask = X86GetElements(context, 0x08090a0b0c0d0e0fL, 0x0001020304050607L);
                                    return context.AddIntrinsic(Intrinsic.X86Pshufb, op1, mask);
                                case 1:
                                    mask = X86GetElements(context, 0x09080b0a0d0c0f0eL, 0x0100030205040706L);
                                    return context.AddIntrinsic(Intrinsic.X86Pshufb, op1, mask);
                                case 2:
                                    return context.AddIntrinsic(Intrinsic.X86Shufps, op1, op1, Const(1 | (0 << 2) | (3 << 4) | (2 << 6)));
                            }
                            break;
                        case 2:
                            // Rev32
                            switch (op.Opc)
                            {
                                case 0:
                                    mask = X86GetElements(context, 0x0c0d0e0f_08090a0bL, 0x04050607_00010203L);
                                    return context.AddIntrinsic(Intrinsic.X86Pshufb, op1, mask);
                                case 1:
                                    mask = X86GetElements(context, 0x0d0c0f0e_09080b0aL, 0x05040706_01000302L);
                                    return context.AddIntrinsic(Intrinsic.X86Pshufb, op1, mask);
                            }
                            break;
                        case 1:
                            // Rev16
                            mask = X86GetElements(context, 0x0e0f_0c0d_0a0b_0809L, 0x_0607_0405_0203_0001L);
                            return context.AddIntrinsic(Intrinsic.X86Pshufb, op1, mask);
                    }

                    throw new InvalidOperationException("Unknown VREV Opcode + Size combo.");
                });
            }
            else
            {
                EmitVectorUnaryOpZx32(context, (op1) =>
                {
                    switch (op.Opc)
                    {
                        case 0:
                            switch (op.Size) // Swap bytes.
                            {
                                case 1:
                                    return InstEmitAluHelper.EmitReverseBytes16_32Op(context, op1);
                                case 2:
                                case 3:
                                    return context.ByteSwap(op1);
                            }
                            break;
                        case 1:
                            switch (op.Size)
                            {
                                case 2:
                                    return context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(op1, Const(0xffff0000)), Const(16)),
                                                                context.ShiftLeft(context.BitwiseAnd(op1, Const(0x0000ffff)), Const(16)));
                                case 3:
                                    return context.BitwiseOr(
                                        context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(op1, Const(0xffff000000000000ul)), Const(48)),
                                                             context.ShiftLeft(context.BitwiseAnd(op1, Const(0x000000000000fffful)), Const(48))),
                                        context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(op1, Const(0x0000ffff00000000ul)), Const(16)),
                                                             context.ShiftLeft(context.BitwiseAnd(op1, Const(0x00000000ffff0000ul)), Const(16))));
                            }
                            break;
                        case 2:
                            // Swap upper and lower halves.
                            return context.BitwiseOr(context.ShiftRightUI(context.BitwiseAnd(op1, Const(0xffffffff00000000ul)), Const(32)),
                                                        context.ShiftLeft(context.BitwiseAnd(op1, Const(0x00000000fffffffful)), Const(32)));
                    }

                    throw new InvalidOperationException("Unknown VREV Opcode + Size combo.");
                });
            }
        }

        public static void Vrecpe(ArmEmitterContext context)
        {
            OpCode32SimdSqrte op = (OpCode32SimdSqrte)context.CurrOp;

            if (op.F)
            {
                int sizeF = op.Size & 1;

                if (Optimizations.FastFP && Optimizations.UseSse2 && sizeF == 0)
                {
                    EmitVectorUnaryOpF32(context, Intrinsic.X86Rcpps, 0);
                }
                else
                {
                    EmitVectorUnaryOpF32(context, (op1) =>
                    {
                        return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPRecipEstimateFpscr, SoftFloat64.FPRecipEstimateFpscr, op1);
                    });
                }
            }
            else
            {
                throw new NotImplementedException("Integer Vrecpe not currently implemented.");
            }
        }

        public static void Vrecps(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;
                bool single = (op.Size & 1) == 0;

                // (2 - (n*m))
                EmitVectorBinaryOpSimd32(context, (n, m) =>
                {
                    if (single)
                    {
                        Operand maskTwo = X86GetAllElements(context, 2f);

                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulps, n, m);

                        return context.AddIntrinsic(Intrinsic.X86Subps, maskTwo, res);
                    }
                    else
                    {
                        Operand maskTwo = X86GetAllElements(context, 2d);

                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulpd, n, m);

                        return context.AddIntrinsic(Intrinsic.X86Subpd, maskTwo, res);
                    }
                });
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPRecipStep, SoftFloat64.FPRecipStep, op1, op2);
                });
            }
        }

        public static void Vrsqrte(ArmEmitterContext context)
        {
            OpCode32SimdSqrte op = (OpCode32SimdSqrte)context.CurrOp;

            if (op.F)
            {
                int sizeF = op.Size & 1;

                if (Optimizations.FastFP && Optimizations.UseSse2 && sizeF == 0)
                {
                    EmitVectorUnaryOpF32(context, Intrinsic.X86Rsqrtps, 0);
                }
                else
                {
                    EmitVectorUnaryOpF32(context, (op1) =>
                    {
                        return EmitSoftFloatCallDefaultFpscr(context, SoftFloat32.FPRSqrtEstimateFpscr, SoftFloat64.FPRSqrtEstimateFpscr, op1);
                    });
                }
            }
            else
            {
                throw new NotImplementedException("Integer Vrsqrte not currently implemented.");
            }
        }

        public static void Vrsqrts(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;
                bool single = (op.Size & 1) == 0;

                // (3 - (n*m)) / 2
                EmitVectorBinaryOpSimd32(context, (n, m) =>
                {
                    if (single)
                    {
                        Operand maskHalf = X86GetAllElements(context, 0.5f);
                        Operand maskThree = X86GetAllElements(context, 3f);

                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulps, n, m);

                        res = context.AddIntrinsic(Intrinsic.X86Subps, maskThree, res);
                        return context.AddIntrinsic(Intrinsic.X86Mulps, maskHalf, res);
                    }
                    else
                    {
                        Operand maskHalf = X86GetAllElements(context, 0.5d);
                        Operand maskThree = X86GetAllElements(context, 3d);

                        Operand res = context.AddIntrinsic(Intrinsic.X86Mulpd, n, m);

                        res = context.AddIntrinsic(Intrinsic.X86Subpd, maskThree, res);
                        return context.AddIntrinsic(Intrinsic.X86Mulpd, maskHalf, res);
                    }
                });
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPRSqrtStep, SoftFloat64.FPRSqrtStep, op1, op2);
                });
            }
        }

        public static void Vsel(ArmEmitterContext context)
        {
            OpCode32SimdSel op = (OpCode32SimdSel)context.CurrOp;

            Operand condition = null;
            switch (op.Cc)
            {
                case OpCode32SimdSelMode.Eq:
                    condition = GetCondTrue(context, Condition.Eq);
                    break;
                case OpCode32SimdSelMode.Ge:
                    condition = GetCondTrue(context, Condition.Ge);
                    break;
                case OpCode32SimdSelMode.Gt:
                    condition = GetCondTrue(context, Condition.Gt);
                    break;
                case OpCode32SimdSelMode.Vs:
                    condition = GetCondTrue(context, Condition.Vs);
                    break;
            }

            EmitScalarBinaryOpI32(context, (op1, op2) =>
            {
                return context.ConditionalSelect(condition, op1, op2);
            });
        }

        public static void Vsqrt_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarUnaryOpF32(context, Intrinsic.X86Sqrtss, Intrinsic.X86Sqrtsd);
            }
            else
            {
                EmitScalarUnaryOpF32(context, (op1) =>
                {
                    return EmitSoftFloatCall(context, SoftFloat32.FPSqrt, SoftFloat64.FPSqrt, op1);
                });
            }
        }

        public static void Vsub_S(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitScalarBinaryOpF32(context, Intrinsic.X86Subss, Intrinsic.X86Subsd);
            }
            else
            {
                EmitScalarBinaryOpF32(context, (op1, op2) => context.Subtract(op1, op2));
            }
        }

        public static void Vsub_V(ArmEmitterContext context)
        {
            if (Optimizations.FastFP && Optimizations.UseSse2)
            {
                EmitVectorBinaryOpF32(context, Intrinsic.X86Subps, Intrinsic.X86Subpd);
            }
            else
            {
                EmitVectorBinaryOpF32(context, (op1, op2) => context.Subtract(op1, op2));
            }
        }

        public static void Vsub_I(ArmEmitterContext context)
        {
            if (Optimizations.UseSse2)
            {
                OpCode32SimdReg op = (OpCode32SimdReg)context.CurrOp;
                EmitVectorBinaryOpSimd32(context, (op1, op2) => context.AddIntrinsic(X86PsubInstruction[op.Size], op1, op2));
            }
            else
            {
                EmitVectorBinaryOpZx32(context, (op1, op2) => context.Subtract(op1, op2));
            }
        }

        private static void EmitSse41MaxMinNumOpF32(ArmEmitterContext context, bool isMaxNum, bool scalar)
        {
            IOpCode32Simd op = (IOpCode32Simd)context.CurrOp;

            Func<Operand, Operand, Operand> genericEmit = (n, m) =>
            {
                Operand nNum = context.Copy(n);
                Operand mNum = context.Copy(m);

                Operand nQNaNMask = InstEmit.EmitSse2VectorIsQNaNOpF(context, nNum);
                Operand mQNaNMask = InstEmit.EmitSse2VectorIsQNaNOpF(context, mNum);

                int sizeF = op.Size & 1;

                if (sizeF == 0)
                {
                    Operand negInfMask = X86GetAllElements(context, isMaxNum ? float.NegativeInfinity : float.PositiveInfinity);

                    Operand nMask = context.AddIntrinsic(Intrinsic.X86Andnps, mQNaNMask, nQNaNMask);
                    Operand mMask = context.AddIntrinsic(Intrinsic.X86Andnps, nQNaNMask, mQNaNMask);

                    nNum = context.AddIntrinsic(Intrinsic.X86Blendvps, nNum, negInfMask, nMask);
                    mNum = context.AddIntrinsic(Intrinsic.X86Blendvps, mNum, negInfMask, mMask);

                    return context.AddIntrinsic(isMaxNum ? Intrinsic.X86Maxps : Intrinsic.X86Minps, nNum, mNum);
                }
                else /* if (sizeF == 1) */
                {
                    Operand negInfMask = X86GetAllElements(context, isMaxNum ? double.NegativeInfinity : double.PositiveInfinity);

                    Operand nMask = context.AddIntrinsic(Intrinsic.X86Andnpd, mQNaNMask, nQNaNMask);
                    Operand mMask = context.AddIntrinsic(Intrinsic.X86Andnpd, nQNaNMask, mQNaNMask);

                    nNum = context.AddIntrinsic(Intrinsic.X86Blendvpd, nNum, negInfMask, nMask);
                    mNum = context.AddIntrinsic(Intrinsic.X86Blendvpd, mNum, negInfMask, mMask);

                    return context.AddIntrinsic(isMaxNum ? Intrinsic.X86Maxpd : Intrinsic.X86Minpd, nNum, mNum);
                }
            };

            if (scalar)
            {
                EmitScalarBinaryOpSimd32(context, genericEmit);
            }
            else
            {
                EmitVectorBinaryOpSimd32(context, genericEmit);
            }
        }
    }
}
