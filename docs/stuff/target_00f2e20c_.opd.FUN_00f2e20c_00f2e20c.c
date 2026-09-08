// original call TARGET 0x00f2e20c

undefined4 _opd_FUN_00f2e20c(int param_1,uint *param_2)

{
  uint uVar1;
  uint uVar2;
  undefined4 uVar3;
  longlong lVar4;
  
  uVar3 = 0xfffffe0b;
  if (*(int *)(param_1 + 0x454) < 0x3fd) {
    lVar4 = 4;
    uVar2 = 0x18;
    *param_2 = 0;
    do {
      uVar1 = uVar2 & 0x3f;
      uVar2 = uVar2 - 8;
      *param_2 = *param_2 | (uint)*(byte *)(param_1 + *(int *)(param_1 + 0x454) + 0x40) << uVar1;
      *(int *)(param_1 + 0x454) = *(int *)(param_1 + 0x454) + 1;
      lVar4 = lVar4 + -1;
    } while (lVar4 != 0);
    uVar3 = 0;
  }
  return uVar3;
}

