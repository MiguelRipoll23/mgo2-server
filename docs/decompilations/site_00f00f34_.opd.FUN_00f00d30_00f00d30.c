// hook SITE 0x00f00f34

int _opd_FUN_00f00d30(int *param_1)

{
  uint uVar1;
  undefined4 *puVar2;
  undefined *puVar3;
  uint *puVar4;
  int iVar5;
  int iVar6;
  int iVar7;
  uint auStack_c0 [32];
  
  puVar3 = PTR_PTR_0121c4d0;
  param_1[0x53] = -1;
  _opd_FUN_00f9ac38(auStack_c0,param_1 + 0x33,0x80);
  if (param_1[0x53] < *param_1) {
    param_1[0x53] = *param_1;
  }
  if (param_1[0x53] < param_1[0x11]) {
    param_1[0x53] = param_1[0x11];
  }
  if (param_1[0x53] < param_1[0x22]) {
    param_1[0x53] = param_1[0x22];
  }
  if ((param_1[0x53] == -1) || (iVar5 = _opd_FUN_00f00b30(param_1[0x53] + 1,auStack_c0), iVar5 == 0)
     ) {
    iVar5 = 0;
  }
  else {
    iVar5 = -1;
    iVar7 = 0;
    iVar6 = 0;
    do {
      puVar4 = (uint *)(iVar6 + (int)param_1);
      uVar1 = *puVar4;
      if ((0 < (int)uVar1) && ((1 << (uVar1 & 0x1f) & auStack_c0[(int)uVar1 >> 5]) != 0)) {
        iVar5 = _opd_FUN_00f00a5c(iVar7 * 0x480 + param_1[0xa52a],param_1 + iVar7 * 0x11);
        if (iVar5 == -99) {
          _opd_FUN_00f00ce4(param_1);
          puVar4[2] = 0;
          *puVar4 = 0xffffffff;
          puVar4[1] = 0;
          _opd_FUN_00effa0c(param_1,0x41,0xffffff9d);
        }
        else if (iVar5 != -0x40) {
          if ((iVar5 == 0) ||
             (puVar2 = *(undefined4 **)(*(int *)(puVar3 + -0x8000) + 4), puVar2 == (undefined4 *)0x0
             )) {
            if (iVar7 == 1) {
              iVar5 = _opd_FUN_00f02e9c(param_1);
            }
            else if (iVar7 == 2) {
              iVar5 = _opd_FUN_00f0494c(param_1);
            }
            else if (iVar7 == 0) {
              iVar5 = _opd_FUN_00f01f48(param_1);
            }
            if ((iVar5 == 0) &&
               (puVar2 = (undefined4 *)**(int **)(puVar3 + -0x8000), puVar2 != (undefined4 *)0x0)) {
              (*(code *)*puVar2)(param_1,iVar7);
            }
          }
          else {
            (*(code *)*puVar2)(param_1,iVar7,iVar5);
          }
        }
      }
      iVar7 = iVar7 + 1;
      iVar6 = iVar6 + 0x44;
    } while (iVar7 != 3);
  }
  return iVar5;
}

