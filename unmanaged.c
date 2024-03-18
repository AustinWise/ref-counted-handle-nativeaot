#include <stdint.h>
#include <stdio.h>

int32_t MyAddRef(void* obj);
int32_t MyRelease(void* obj);
int32_t GetObjectInfo(void* obj);


void MyUnmanagedFunction(void* obj)
{
    int32_t info = GetObjectInfo(obj);
    printf("native info: %d\n", info);
    MyRelease(obj);
}