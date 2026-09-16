// original call TARGET 0x00fa4d48

void _opd_FUN_00fa4d48(undefined8 *param_1,undefined1 param_2,uint param_3)

{
  undefined2 *puVar1;
  undefined4 *puVar2;
  undefined2 uVar3;
  undefined4 uVar4;
  undefined8 *puVar5;
  undefined8 uVar6;
  int iVar7;
  undefined1 *puVar8;
  int iVar9;
  
  puVar5 = (undefined8 *)((int)param_1 + param_3);
  if (7 < param_3) {
    *(undefined1 *)param_1 = param_2;
    puVar1 = (undefined2 *)((uint)((int)param_1 + 1) & 0xfffffffe);
    uVar3 = CONCAT11(param_2,param_2);
    puVar2 = (undefined4 *)((uint)(puVar1 + 1) & 0xfffffffc);
    *puVar1 = uVar3;
    uVar4 = CONCAT22(uVar3,uVar3);
    *puVar2 = uVar4;
    uVar6 = CONCAT44(uVar4,uVar4);
    for (param_1 = (undefined8 *)((uint)(puVar2 + 1) & 0xfffffff8);
        param_1 <= (undefined8 *)((uint)puVar5 & 0xfffffff8) + -8; param_1 = param_1 + 8) {
      param_1[7] = uVar6;
      param_1[6] = uVar6;
      param_1[5] = uVar6;
      param_1[4] = uVar6;
      param_1[3] = uVar6;
      param_1[2] = uVar6;
      param_1[1] = uVar6;
      *param_1 = uVar6;
    }
    for (; param_1 < (undefined8 *)((uint)puVar5 & 0xfffffff8); param_1 = param_1 + 1) {
      *param_1 = uVar6;
    }
  }
  if (puVar5 <= param_1) {
    return;
  }
  iVar7 = (int)puVar5 - (int)param_1;
  iVar9 = 0;
  do {
    puVar8 = (undefined1 *)((int)param_1 + iVar9);
    iVar9 = iVar9 + 1;
    *puVar8 = param_2;
    iVar7 = iVar7 + -1;
  } while (iVar7 != 0);
  return;
}

