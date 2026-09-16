// original call TARGET 0x0026cc88

int _opd_FUN_0026cc88(ushort *param_1,undefined1 *param_2,int param_3)

{
  int iVar1;
  undefined1 *puVar2;
  ushort uVar3;
  int iVar4;
  
  uVar3 = *param_1;
  iVar1 = (uint)param_1[1] - (uint)uVar3;
  if (param_3 < iVar1) {
    iVar1 = param_3;
  }
  if (0 < iVar1) {
    puVar2 = (undefined1 *)(*(int *)(param_1 + 4) + (uint)uVar3 + iVar1);
    iVar4 = iVar1;
    do {
      puVar2 = puVar2 + -1;
      *param_2 = *puVar2;
      param_2 = param_2 + 1;
      iVar4 = iVar4 + -1;
    } while (iVar4 != 0);
    uVar3 = *param_1;
  }
  *param_1 = (short)iVar1 + uVar3;
  return iVar1;
}

