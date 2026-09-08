// original call TARGET 0x0026cc10

int _opd_FUN_0026cc10(ushort *param_1,int param_2,int param_3)

{
  uint uVar1;
  undefined1 *puVar2;
  undefined1 *puVar3;
  ushort uVar4;
  int iVar5;
  
  uVar1 = (uint)*param_1;
  if (0 < param_3) {
    puVar2 = (undefined1 *)(param_2 + param_3);
    puVar3 = (undefined1 *)(*(int *)(param_1 + 4) + uVar1);
    iVar5 = param_3;
    do {
      puVar2 = puVar2 + -1;
      *puVar3 = *puVar2;
      iVar5 = iVar5 + -1;
      puVar3 = puVar3 + 1;
    } while (iVar5 != 0);
    uVar1 = (uint)*param_1;
  }
  uVar4 = (ushort)(param_3 + uVar1);
  *param_1 = uVar4;
  if ((uint)param_1[1] < (param_3 + uVar1 & 0xffff)) {
    param_1[1] = uVar4;
  }
  return param_3;
}

