// original call TARGET 0x00f00a00

int _opd_FUN_00f00a00(int param_1,undefined4 *param_2)

{
  int iVar1;
  uint uVar2;
  
  iVar1 = param_2[2];
  param_2[2] = iVar1 + 1;
  *(int *)(param_1 + 8) = iVar1 + 1;
  uVar2 = _opd_FUN_00f2e904(param_1,*param_2);
  return ((int)((((int)uVar2 >> 0x1f ^ uVar2) - ((int)uVar2 >> 0x1f)) + -1) >> 0x1f & 0x3dU) - 0x3d;
}

