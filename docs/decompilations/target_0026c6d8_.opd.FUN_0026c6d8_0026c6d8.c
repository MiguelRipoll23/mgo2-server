// original call TARGET 0x0026c6d8

undefined4 _opd_FUN_0026c6d8(void)

{
  undefined4 *puVar1;
  undefined *puVar2;
  int iVar3;
  undefined4 uVar4;
  undefined1 local_30 [24];
  
  puVar2 = PTR_PTR_0121af54;
  puVar1 = *(undefined4 **)(PTR_PTR_0121af54 + -0x8000);
  iVar3 = FUN_00fbb55c(local_30);
  uVar4 = 0xffffffff;
  if (-1 < iVar3) {
    iVar3 = FUN_00fbb99c();
    uVar4 = 0xffffffff;
    if (-1 < iVar3) {
      iVar3 = FUN_00fbb95c(puVar1 + 4);
      uVar4 = 0xffffffff;
      if (iVar3 == 0) {
        iVar3 = FUN_00fbb8dc(*(undefined4 *)(puVar2 + -0x7ffc),puVar1,puVar1 + 5);
        uVar4 = 0xffffffff;
        if (iVar3 == 0) {
          uVar4 = _opd_FUN_0026d350(0x20000,1);
          iVar3 = FUN_00fbd05c(0x20000,uVar4);
          uVar4 = 0xffffffff;
          if (-1 < iVar3) {
            iVar3 = FUN_00fbd0bc(*(undefined4 *)(puVar2 + -0x7ff8),0);
            uVar4 = 0xffffffff;
            if (-1 < iVar3) {
              *(undefined1 *)(puVar1 + 6) = 0;
              *puVar1 = 1;
              uVar4 = 0;
            }
          }
        }
      }
    }
  }
  return uVar4;
}

