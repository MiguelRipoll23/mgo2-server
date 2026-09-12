// hook SITE 0x00262e9c

void _opd_FUN_00262d10(int param_1,int param_2)

{
  int *piVar1;
  undefined *puVar2;
  int iVar3;
  int iVar4;
  int iVar5;
  int iVar6;
  
  puVar2 = PTR_PTR_0121af44;
  piVar1 = *(int **)(PTR_PTR_0121af44 + -0x8000);
  if (*piVar1 == 0) {
    iVar4 = 0;
    piVar1[9] = param_2;
    piVar1[8] = param_1;
    piVar1[10] = param_1;
    iVar6 = param_1;
    iVar3 = param_1 + 0xa440;
    do {
      iVar5 = iVar3;
      if (param_2 <= iVar4 + 0xa440) {
        return;
      }
      if (iVar6 != 0) {
        _opd_FUN_00263b70(iVar6,iVar5,0x4000,0x2000,0x2000);
      }
      iVar4 = iVar4 + 0x8000;
      iVar6 = iVar6 + 0x6d8;
      iVar3 = iVar5 + 0x8000;
    } while (iVar4 != 0xc0000);
    iVar6 = iVar5 + 0xc000;
    if (iVar6 - param_1 < param_2) {
      piVar1[0xd] = iVar6;
      param_1 = (iVar5 + 0x1c000) - param_1;
      if (param_1 < param_2) {
        _opd_FUN_00fa4d48(iVar6,0,0x10000);
        _opd_FUN_0026d190(iVar5 + 0x1c000,param_2 - param_1);
        **(undefined4 **)(puVar2 + -0x7ffc) = 0x7d000;
        *piVar1 = 1;
        piVar1[0xe] = 3;
        _opd_FUN_0026c9e0(piVar1 + 4);
        _opd_FUN_0026ca78(piVar1 + 4,piVar1 + 6);
        piVar1[2] = 0;
        piVar1[0x22] = 0;
        piVar1[0x23] = 0;
        _opd_FUN_0026c6d8();
        piVar1[0x17] = 0;
        piVar1[0x18] = 0;
        piVar1[0x15] = 0;
        piVar1[0x16] = 0;
      }
    }
  }
  return;
}

