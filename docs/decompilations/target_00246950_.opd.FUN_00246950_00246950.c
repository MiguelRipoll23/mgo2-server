// original call TARGET 0x00246950

uint _opd_FUN_00246950(int param_1)

{
  uint uVar1;
  
  uVar1 = 0xffffffff;
  if (param_1 != 0) {
    uVar1 = (int)*(uint *)(param_1 + 0xf8) >> 0x1f;
    uVar1 = uVar1 - (uVar1 ^ *(uint *)(param_1 + 0xf8)) >> 0x1f;
  }
  return uVar1;
}

