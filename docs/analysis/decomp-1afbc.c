// function FUN_0001afbc @ 0001afbc (target 1b200)

void FUN_0001afbc(void)

{
  undefined8 *puVar1;
  int iVar3;
  undefined8 uVar2;
  undefined4 uVar4;
  int iVar5;
  int iVar6;
  int *piVar7;
  undefined8 unaff_r27;
  longlong lVar8;
  undefined8 uVar9;
  longlong lVar10;
  
  iVar3 = FUN_00195a08();
  lVar8 = *(longlong *)(iVar3 + 0x28);
  while (lVar8 != 0) {
    puVar1 = (undefined8 *)lVar8;
    uVar9 = *puVar1;
    lVar8 = puVar1[2];
    iVar3 = FUN_001951f8(puVar1[1]);
    if (iVar3 == 5) {
      unaff_r27 = FUN_00195b90(unaff_r27,uVar9);
      iVar3 = FUN_00196d7c();
      piVar7 = (int *)uRam001ec478;
      *piVar7 = iVar3;
      if (0 < iVar3) {
        iVar3 = 0;
        lVar10 = lRam001ec480;
        do {
          uVar9 = FUN_00196e9c(unaff_r27,iVar3);
          FUN_00196e9c(uVar9,0);
          uVar2 = FUN_0019685c();
          puVar1 = (undefined8 *)lVar10;
          *puVar1 = uVar2;
          FUN_00196e9c(uVar9,1);
          uVar2 = FUN_0019685c();
          puVar1[1] = uVar2;
          FUN_00196e9c(uVar9,2);
          uVar4 = FUN_00196000();
          *(undefined4 *)(puVar1 + 2) = uVar4;
          FUN_00196e9c(uVar9,3);
          uVar4 = FUN_00196000();
          *(undefined4 *)((int)puVar1 + 0x14) = uVar4;
          FUN_00196e9c(uVar9,4);
          iVar5 = FUN_00195e18();
          *(bool *)(puVar1 + 3) = iVar5 != 0;
          FUN_00196e9c(uVar9,5);
          iVar6 = FUN_00195e18();
          iVar5 = *piVar7;
          iVar3 = iVar3 + 1;
          *(bool *)((int)puVar1 + 0x19) = iVar6 != 0;
          lVar10 = lVar10 + 0x20;
        } while (iVar3 < iVar5);
      }
    }
  }
  return;
}

