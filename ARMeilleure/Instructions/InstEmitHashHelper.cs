using ARMeilleure.IntermediateRepresentation;
using ARMeilleure.Translation;
using System.Diagnostics;

using static ARMeilleure.IntermediateRepresentation.OperandHelper;

namespace ARMeilleure.Instructions
{
    static class InstEmitHashHelper
    {
        public const uint Crc32RevPoly = 0xedb88320;
        public const uint Crc32cRevPoly = 0x82f63b78;

        public static Operand EmitCrc32(ArmEmitterContext context, Operand crc, Operand value, int size, bool c)
        {
            Debug.Assert(crc.Type == OperandType.I32);
            Debug.Assert(size >= 0 && size < 4);

            if (c && Optimizations.UseSse42)
            {
                Intrinsic op = size switch
                {
                    0 => Intrinsic.X86Crc32_8,
                    1 => Intrinsic.X86Crc32_16,
                    _ => Intrinsic.X86Crc32,
                };

                return context.AddIntrinsicInt(op, crc, value);
            }
            else
            {
                Operand poly = Const(c ? Crc32cRevPoly : Crc32RevPoly);
                int bytes = 1 << size;
                Operand one = Const(1);

                for (int i = 0; i < bytes; i++)
                {
                    Operand val = context.ZeroExtend8(OperandType.I32, context.ShiftRightUI(value, Const(i * 8)));
                    crc = context.BitwiseExclusiveOr(crc, val);
                    for (int k = 0; k < 8; k++)
                    {
                        // crc = (crc >> 1) ^ (poly & (0 - (crc & 1)));
                        crc = context.BitwiseExclusiveOr(context.ShiftRightUI(crc, one), context.BitwiseAnd(poly, context.Negate(context.BitwiseAnd(crc, one))));
                    }
                }

                return crc;
            }
        }
    }
}
