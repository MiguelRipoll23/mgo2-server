// original call TARGET 0x000bead8

void _opd_FUN_000bead8(ulonglong param_1,ulonglong param_2,int param_3,int param_4)

{
  int iVar1;
  int *piVar2;
  int *piVar3;
  uint uVar4;
  undefined *puVar5;
  longlong lVar6;
  int iVar7;
  int iVar8;
  
  puVar5 = PTR_PTR_0121ac7c;
  uVar4 = (uint)(param_2 >> 0x38);
  piVar3 = (int *)param_1;
  if (uVar4 == 0) {
    iVar7 = *(int *)(PTR_PTR_0121ac7c + -0x7ff4);
    piVar3[1] = param_3;
    piVar3[7] = param_4;
    piVar3[3] = (int)param_2;
    iVar1 = *(int *)(puVar5 + -0x8000);
    iVar8 = iVar1 + 0x6500;
    *(undefined8 *)(piVar3 + 0x10) = *(undefined8 *)(iVar7 + 0x10);
  }
  else {
    iVar1 = *(int *)(PTR_PTR_0121ac7c + -0x8000);
    iVar7 = *(int *)(PTR_PTR_0121ac7c + -0x7ff4);
    piVar3[1] = param_3;
    piVar3[7] = param_4;
    piVar3[3] = (int)param_2;
    iVar8 = (uVar4 - 1) * 0x10d0 + iVar1 + 0x20;
    *(undefined8 *)(piVar3 + 0x10) = *(undefined8 *)(iVar7 + 0x10);
  }
  if ((param_2 & 0x1000) == 0) {
    iVar7 = _opd_FUN_000d3780();
    piVar3[4] = iVar7;
  }
  else {
    piVar3[4] = 0;
  }
  lVar6 = *(longlong *)(iVar1 + 8);
  *(ulonglong *)(iVar1 + 8) = lVar6 + 1U & 0xfffffffff;
  piVar2 = *(int **)(iVar8 + 0x78);
  *(ulonglong *)(piVar3 + 8) = lVar6 << 0x1c | param_1 >> 4 & 0xfffffff;
  piVar3[6] = (int)piVar2;
  *(int **)(iVar8 + 0x78) = piVar3;
  *piVar2 = (int)piVar3;
  *piVar3 = iVar8 + 0x60;
  return;
}

