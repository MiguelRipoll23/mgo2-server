// original call TARGET 0x00333608

uint _opd_FUN_00333608(int param_1,uint param_2,uint param_3)

{
  uint uVar1;
  uint uVar2;
  longlong *plVar3;
  longlong *plVar4;
  
  uVar2 = 0;
  uVar1 = *(uint *)(*(int *)(param_1 + 0x364) + param_3 * 4 + 0x34);
  plVar4 = (longlong *)
           (param_1 +
           param_2 * 0x40 + (param_2 & 0xfffffff) * -0x10 +
           param_3 * 0x80 + (param_3 & 0x7ffffff) * -0x20);
  plVar3 = (longlong *)(param_1 + uVar1 * 0x40 + (uVar1 & 0xfffffff) * -0x10 + 0x240);
  if ((((*plVar4 == *plVar3) && (plVar4[1] == plVar3[1])) &&
      (*(int *)(plVar4 + 2) == *(int *)(plVar3 + 2))) &&
     (*(int *)((int)plVar4 + 0x14) == *(int *)((int)plVar3 + 0x14))) {
    uVar2 = *(uint *)(uVar1 * 0x90 + *(int *)(*(int *)(*(int *)(param_1 + 0x360) + 4) + 4) + 4) & 1;
  }
  return uVar2;
}

